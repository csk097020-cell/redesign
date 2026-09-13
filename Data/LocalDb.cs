using SQLite;
using System.Text.Json;
using MomentaryMomentos.Models;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.Data;

/// <summary>
/// Lightweight offline cache (and fallback) using sqlite-net-pcl with sync queue support.
/// </summary>
public sealed class LocalDb
{
    private readonly SQLiteAsyncConnection _db;

    public LocalDb()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "momentarymomentos.db3");
        _db = new SQLiteAsyncConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        _db.CreateTableAsync<MemoryRow>().Wait();
        _db.CreateTableAsync<TagRow>().Wait();
        _db.CreateTableAsync<PendingUploadRow>().Wait();
        MigrateAsync().Wait();
    }

    private async Task MigrateAsync()
    {
        // ConfigureAwait(false) is required — this is called via .Wait() in the constructor,
        // so continuations must NOT marshal back to the UI thread or they will deadlock.
        // ADD COLUMN is idempotent — SQLite throws if the column already exists, so we swallow.
        try { await _db.ExecuteAsync("ALTER TABLE momo_memories ADD COLUMN LocalThumbnailPath TEXT").ConfigureAwait(false); } catch { }
        try { await _db.ExecuteAsync("ALTER TABLE momo_memories ADD COLUMN LocalVideoPath TEXT").ConfigureAwait(false); } catch { }
        try { await _db.ExecuteAsync("ALTER TABLE momo_memories ADD COLUMN Caption TEXT").ConfigureAwait(false); } catch { }
        try { await _db.ExecuteAsync("ALTER TABLE momo_memories ADD COLUMN DateCaptured TEXT").ConfigureAwait(false); } catch { }
    }

    // ----- Tags -----

    public Task UpsertTagsAsync(IEnumerable<Tag> tags) =>
        _db.RunInTransactionAsync(tran =>
        {
            foreach (var t in tags)
            {
                tran.InsertOrReplace(new TagRow
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color,
                    Icon = t.Icon,
                    IsActive = t.IsActive,
                    UserId = t.UserId
                });
            }
        });

    public async Task<List<Tag>> GetTagsAsync(string? userId, bool includeInactive = false)
    {
        var rows = await _db.Table<TagRow>().ToListAsync();
        var filtered = rows.Where(r => r.UserId == null || r.UserId == userId);
        if (!includeInactive) filtered = filtered.Where(r => r.IsActive);
        return filtered
            .OrderBy(r => r.Name)
            .Select(r => new Tag
            {
                Id = r.Id,
                Name = r.Name,
                Color = r.Color,
                Icon = r.Icon,
                IsActive = r.IsActive,
                UserId = r.UserId
            })
            .ToList();
    }

    // ----- Memories -----

    public async Task UpsertMemoryAsync(Memory memory)
    {
        await _db.InsertOrReplaceAsync(new MemoryRow
        {
            Id = memory.Id,
            UserId = memory.UserId,
            Title = memory.Title,
            Caption = memory.Caption,
            VideoUrl = memory.VideoUrl,
            ThumbnailUrl = memory.ThumbnailUrl,
            LocalThumbnailPath = memory.LocalThumbnailPath,
            LocalVideoPath = memory.LocalVideoPath,
            TagsJson = JsonSerializer.Serialize(memory.Tags, Json.Options),
            IsFavorite = memory.IsFavorite,
            CreatedAtUtc = memory.CreatedAt.UtcDateTime,
            DateCaptured = DateOnlyToText(memory.DateCaptured)
        });
    }

    public async Task UpsertMemoriesAsync(IEnumerable<Memory> memories)
    {
        var memoryList = memories.ToList();
        if (memoryList.Count == 0) return;

        var existingRows = await _db.Table<MemoryRow>().ToListAsync();
        var existingById = existingRows.ToDictionary(r => r.Id, r => r);

        await _db.RunInTransactionAsync(tran =>
        {
            foreach (var m in memoryList)
            {
                existingById.TryGetValue(m.Id, out var existing);
                tran.InsertOrReplace(new MemoryRow
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    Title = m.Title,
                    Caption = m.Caption,
                    VideoUrl = m.VideoUrl,
                    ThumbnailUrl = m.ThumbnailUrl,
                    LocalThumbnailPath = m.LocalThumbnailPath ?? existing?.LocalThumbnailPath,
                    LocalVideoPath = m.LocalVideoPath ?? existing?.LocalVideoPath,
                    TagsJson = JsonSerializer.Serialize(m.Tags, Json.Options),
                    IsFavorite = m.IsFavorite,
                    CreatedAtUtc = m.CreatedAt.UtcDateTime,
                    DateCaptured = DateOnlyToText(m.DateCaptured)
                });
            }
        });
    }

    public async Task<List<Memory>> GetMemoriesAsync(string userId)
    {
        var rows = await _db.Table<MemoryRow>().Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
        return rows.Select(r => new Memory
        {
            Id = r.Id,
            UserId = r.UserId,
            Title = r.Title,
            Caption = r.Caption,
            VideoUrl = r.VideoUrl,
            ThumbnailUrl = r.ThumbnailUrl,
            LocalThumbnailPath = ExistingFileOrNull(r.LocalThumbnailPath),
            LocalVideoPath = r.LocalVideoPath,
            Tags = JsonSerializer.Deserialize<List<string>>(r.TagsJson ?? "[]", Json.Options) ?? new(),
            IsFavorite = r.IsFavorite,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc)),
            DateCaptured = TextToDateOnly(r.DateCaptured)
        }).ToList();
    }

    public Task UpdateLocalThumbnailAsync(string memoryId, string? path) =>
        _db.ExecuteAsync("UPDATE momo_memories SET LocalThumbnailPath = ? WHERE Id = ?", path, memoryId);

    public Task UpdateFavoriteAsync(string id, bool isFavorite) =>
        _db.ExecuteAsync("UPDATE momo_memories SET IsFavorite = ? WHERE Id = ?", isFavorite ? 1 : 0, id);

    public Task UpdateTagsAsync(string id, List<string> tags) =>
        _db.ExecuteAsync("UPDATE momo_memories SET TagsJson = ? WHERE Id = ?", JsonSerializer.Serialize(tags, Json.Options), id);

    public Task DeleteMemoryAsync(string id) =>
        _db.ExecuteAsync("DELETE FROM momo_memories WHERE Id = ?", id);

    // ----- Pending Uploads (Sync Queue) -----

    public async Task MarkMemoryAsPendingUploadAsync(string memoryId, string? localVideoPath = null)
    {
        await _db.InsertOrReplaceAsync(new PendingUploadRow
        {
            MemoryId = memoryId,
            LocalVideoPath = localVideoPath,
            QueuedAtUtc = DateTime.UtcNow,
            RetryCount = 0
        });
    }

    public async Task<List<Memory>> GetPendingUploadMemoriesAsync()
    {
        var pendingRows = await _db.Table<PendingUploadRow>().ToListAsync();
        var memoryIds = pendingRows.Select(p => p.MemoryId).ToList();

        if (memoryIds.Count == 0)
            return new List<Memory>();

        var memoryRows = await _db.Table<MemoryRow>()
            .Where(m => memoryIds.Contains(m.Id))
            .ToListAsync();

        return memoryRows.Select(r => new Memory
        {
            Id = r.Id,
            UserId = r.UserId,
            Title = r.Title,
            Caption = r.Caption,
            VideoUrl = r.VideoUrl,
            ThumbnailUrl = r.ThumbnailUrl,
            LocalThumbnailPath = ExistingFileOrNull(r.LocalThumbnailPath),
            LocalVideoPath = r.LocalVideoPath,
            Tags = JsonSerializer.Deserialize<List<string>>(r.TagsJson ?? "[]", Json.Options) ?? new(),
            IsFavorite = r.IsFavorite,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc)),
            DateCaptured = TextToDateOnly(r.DateCaptured)
        }).ToList();
    }

    public async Task<string?> GetPendingUploadPathAsync(string memoryId)
    {
        var row = await _db.Table<PendingUploadRow>()
            .Where(p => p.MemoryId == memoryId)
            .FirstOrDefaultAsync();

        return row?.LocalVideoPath;
    }

    public Task ClearPendingUploadAsync(string memoryId) =>
        _db.ExecuteAsync("DELETE FROM momo_pending_uploads WHERE MemoryId = ?", memoryId);

    public async Task<int> GetPendingUploadCountAsync() =>
        await _db.Table<PendingUploadRow>().CountAsync();

    public Task IncrementRetryCountAsync(string memoryId) =>
        _db.ExecuteAsync("UPDATE momo_pending_uploads SET RetryCount = RetryCount + 1 WHERE MemoryId = ?", memoryId);

    private static string? ExistingFileOrNull(string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;

    // DateOnly <-> ISO yyyy-MM-dd text (sqlite-net has no native DateOnly support).
    private static string? DateOnlyToText(DateOnly? d) =>
        d?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static DateOnly? TextToDateOnly(string? s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d)
            ? d : null;

    // ----- Tables -----

    [Table("momo_memories")]
    public sealed class MemoryRow
    {
        [PrimaryKey] public string Id { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Caption { get; set; }
        public string? VideoUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? LocalThumbnailPath { get; set; }
        public string? LocalVideoPath { get; set; }
        public string? TagsJson { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>Manual capture date stored as ISO yyyy-MM-dd; null for legacy rows.</summary>
        public string? DateCaptured { get; set; }
    }

    [Table("momo_tags")]
    public sealed class TagRow
    {
        [PrimaryKey] public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#3B82F6";
        public string Icon { get; set; } = "✨";
        public bool IsActive { get; set; }
        public string? UserId { get; set; }
    }

    [Table("momo_pending_uploads")]
    public sealed class PendingUploadRow
    {
        [PrimaryKey] public string MemoryId { get; set; } = "";
        public string? LocalVideoPath { get; set; }
        public DateTime QueuedAtUtc { get; set; }
        public int RetryCount { get; set; }
    }
}
