using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.ViewModels;

// ── Tag filter chip shown in the horizontal scroll row ───────────────────────
public partial class FilterTagChip : ObservableObject
{
    /// <summary>null = "All" (clears filter)</summary>
    public string? TagId { get; init; }
    public string  Name  { get; init; } = "";
    public string  Color { get; init; } = "#3B82F6";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundDisplay))]
    [NotifyPropertyChangedFor(nameof(BorderDisplay))]
    private bool isActive;

    /// <summary>Hex color used for the chip background: full color when active, glass when inactive.</summary>
    public string BackgroundDisplay => IsActive ? Color : "#22FFFFFF";
    /// <summary>Hex color used for the chip border.</summary>
    public string BorderDisplay     => IsActive ? Color : "#44FFFFFF";
}

// ─────────────────────────────────────────────────────────────────────────────
public partial class ReliveViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SyncService _sync;
    private readonly IVideoTrimService? _videoTrim;

    /// <summary>Current AI weights loaded from momo_memory_metrics.</summary>
    private Dictionary<string, int> _memoryWeights = new();

    // All memories for the current user
    public ObservableCollection<Memory>       Memories       { get; } = new();
    // Subset matching the active tag filter (what the CollectionView binds to)
    public ObservableCollection<Memory>       FilteredMemories { get; } = new();
    // Chips shown in the horizontal filter row
    public ObservableCollection<FilterTagChip> FilterTags    { get; } = new();

    [ObservableProperty] private Memory? selectedMemory;
    [ObservableProperty] private bool    shuffleMode = true;
    [ObservableProperty] private string? activeTagFilterId;
    [ObservableProperty] private bool    isChoosingMemory = true;

    public ReliveViewModel(SupabaseService supabase, SyncService sync, IVideoTrimService? videoTrim = null)
    {
        _supabase  = supabase;
        _sync      = sync;
        _videoTrim = videoTrim;
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Refresh()
    {
        await RunBusyAsync(async () =>
        {
            var session = _supabase.Session ?? throw new InvalidOperationException("Not logged in.");

            // Load tags, memories, and weights in parallel.
            // Include inactive tags so chips/filters for deactivated (legacy) tags still resolve.
            var tagsTask    = _sync.GetTagsWithCacheAsync(session.UserId, includeInactive: true);
            var memoriesTask = _sync.GetMemoriesWithCacheAsync(session.UserId);
            var weightsTask  = _supabase.GetMemoryWeightsAsync(session.UserId);

            var tags    = await tagsTask;
            var list    = await memoriesTask;
            _memoryWeights = await weightsTask;

            // A hidden tag pauses its momentos (#8): they leave the feed, filter row,
            // and Surprise Me until the tag is restored in Manage Tags.
            list = MemoryVisibility.ExcludePaused(list, tags);

            var tagLookup = tags.ToDictionary(t => t.Id, t => t);

            // Resolve tag IDs → display info before backfill so DisplayTags are ready
            foreach (var m in list)
            {
                m.DisplayTags = m.Tags
                    .Select(id => tagLookup.TryGetValue(id, out var t)
                        ? new TagInfo(t.Name, t.Color, t.Icon)
                        : null)
                    .OfType<TagInfo>()
                    .ToList();
            }

            // Backfill thumbnails for memories with a local video but no thumbnail yet
            await BackfillMissingThumbnailsAsync(list);

            Memories.Clear();
            foreach (var m in list)
                Memories.Add(m);

            // Build filter chips from tags that appear in at least one memory
            var usedTagIds = list.SelectMany(m => m.Tags).ToHashSet();
            FilterTags.Clear();
            FilterTags.Add(new FilterTagChip
            {
                TagId    = null,
                Name     = "✨ All",
                Color    = "#00B4D8",
                IsActive = string.IsNullOrEmpty(ActiveTagFilterId)
            });
            foreach (var tag in tags.Where(t => usedTagIds.Contains(t.Id)))
            {
                FilterTags.Add(new FilterTagChip
                {
                    TagId    = tag.Id,
                    Name     = $"{tag.Icon} {tag.Name}",
                    Color    = tag.Color,
                    IsActive = tag.Id == ActiveTagFilterId
                });
            }

            ApplyFilter();
        });
    }

    // ── Tag filtering ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectTagFilter(FilterTagChip chip)
    {
        // Tapping the already-active chip clears the filter
        ActiveTagFilterId = chip.TagId == ActiveTagFilterId ? null : chip.TagId;

        foreach (var c in FilterTags)
            c.IsActive = c.TagId == ActiveTagFilterId;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredMemories.Clear();
        IEnumerable<Memory> source = string.IsNullOrEmpty(ActiveTagFilterId)
            ? Memories
            : Memories.Where(m => m.Tags.Contains(ActiveTagFilterId));
        foreach (var m in source)
            FilteredMemories.Add(m);
    }

    // ── Playback ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Play(Memory memory)
    {
        try
        {
            var source = GetPlayableSource(memory);
            if (source is null)
            {
                _ = Toast.Make("No video available for this memory yet.").Show();
                return;
            }

            // Open the detail page in full (multi-memory) mode — same as the
            // Home "Today's Featured Momento" path (HomeViewModel.PlayFeatured).
            // Passing single=true here trimmed the detail page's memory list to a
            // single item, which made the 🎲 dice (FindAnother) a no-op.
            //
            // Scope the route to the current session user (not memory.UserId): the
            // detail page now ignores any route userId and loads from the session,
            // but we pass the session id for consistency with HomeViewModel so no
            // foreign user id is ever placed on the route.
            var userId = _supabase.Session?.UserId ?? "";
            await Shell.Current.GoToAsync(
                $"memorydetail?memoryId={Uri.EscapeDataString(memory.Id)}" +
                $"&userId={Uri.EscapeDataString(userId)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Relive play failed: {ex}");
            _ = Toast.Make("Couldn't open this memory.").Show();
        }
    }

    // ── Favorite ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleFavorite(Memory memory)
    {
        await RunBusyAsync(async () =>
        {
            var newState = !memory.IsFavorite;
            await _supabase.UpdateFavoriteAsync(memory.Id, newState);
            memory.IsFavorite = newState;
            OnPropertyChanged(nameof(Memories));
        });
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Delete(Memory memory)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Delete Memory",
            "This permanently deletes the memory and moves its video file into the deletion queue. It cannot be undone.",
            "Delete",
            "Cancel");
        if (!ok) return;

        await RunBusyAsync(async () =>
        {
            await _supabase.DeleteMemoryWithStorageTrashAsync(memory);
            Memories.Remove(memory);
            FilteredMemories.Remove(memory);
        });
    }

    // ── Surprise Me — AI-weighted random selection ────────────────────────────

    [RelayCommand]
    private async Task SurpriseMe()
    {
        IsChoosingMemory = true;
        if (Memories.Count == 0)
        {
            return;
        }

        var candidates = Memories
            .Where(m => _memoryWeights.GetValueOrDefault(m.Id, 1) > 0
                        && GetPlayableSource(m) is not null)
            .ToList();

        // If everything is suppressed, fall back to full list so Surprise Me never gets stuck
        if (candidates.Count == 0)
            candidates = Memories.Where(m => GetPlayableSource(m) is not null).ToList();

        if (candidates.Count == 0)
        {
            _ = Toast.Make("No playable memories yet.").Show();
            await Shell.Current.GoToAsync("//main/home");
            return;
        }

        // Expand into a weighted pool: each memory appears weight times (min 1).
        // Without the DB table, all weights default to 1 (uniform random).
        var pool = candidates
            .SelectMany(m => Enumerable.Repeat(m, Math.Max(1, _memoryWeights.GetValueOrDefault(m.Id, 1))))
            .ToList();

        var pick = pool[Random.Shared.Next(pool.Count)];
        await Play(pick);
    }

    private static string? GetPlayableSource(Memory memory)
    {
        if (!string.IsNullOrWhiteSpace(memory.VideoUrl)
            && memory.VideoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return memory.VideoUrl;
        }

        if (!string.IsNullOrWhiteSpace(memory.LocalVideoPath) && File.Exists(memory.LocalVideoPath))
            return memory.LocalVideoPath;

        return null;
    }

    private async Task BackfillMissingThumbnailsAsync(List<Memory> memories)
    {
        if (_videoTrim is null) return;

        // Thumbnails needing a cross-device upload this pass, run off the critical path after render.
        var toUpload = new List<(Memory Memory, string ThumbPath)>();

        foreach (var m in memories)
        {
            if (!string.IsNullOrEmpty(m.ThumbnailUrl)) continue;   // already propagated remotely

            var thumbPath = m.LocalThumbnailPath;

            // Generate locally only if we don't already have a usable thumbnail file on disk.
            if (string.IsNullOrEmpty(thumbPath) || !File.Exists(thumbPath))
            {
                if (string.IsNullOrEmpty(m.LocalVideoPath) || !File.Exists(m.LocalVideoPath)) continue;
                try
                {
                    thumbPath = await _videoTrim.GetThumbnailAsync(m.LocalVideoPath);
                    if (string.IsNullOrEmpty(thumbPath)) continue;
                    m.LocalThumbnailPath = thumbPath;
                    await _sync.UpdateLocalThumbnailAsync(m.Id, thumbPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReliveViewModel] BackfillThumbnail: {ex.Message}");
                    continue;
                }
            }

            // Local thumbnail is generated for display regardless of sync state; only queue the
            // cross-device upload for synced rows (local-only/pending rows have no remote row to PATCH).
            if (m.IsSynced)
                toUpload.Add((m, thumbPath));
        }

        // Upload sequentially in the background so the page render isn't blocked by network.
        if (toUpload.Count > 0)
            _ = Task.Run(async () =>
            {
                foreach (var (m, p) in toUpload)
                    await _sync.TryUploadThumbnailAsync(m, p);
            });
    }
}
