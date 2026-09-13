using MomentaryMomentos.Data;
using MomentaryMomentos.Models;
using System.Diagnostics;

namespace MomentaryMomentos.Services;

/// <summary>
/// Syncs remote Supabase content into local cache and handles offline-to-online synchronization.
/// </summary>
public sealed class SyncService
{
    private readonly SupabaseService _supabase;
    private readonly LocalDb _db;
    private readonly ConnectivityService _connectivity;
    private bool _isSyncing;

    // Memory ids whose thumbnail PATCH matched no remote row this session — don't retry (avoids
    // re-uploading orphan thumbnails to storage every backfill pass).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _missingRemoteRow = new();

    public event EventHandler<SyncEventArgs>? SyncCompleted;
    public event EventHandler<SyncEventArgs>? SyncFailed;

    public bool IsSyncing => _isSyncing;

    public SyncService(SupabaseService supabase, LocalDb db, ConnectivityService connectivity)
    {
        _supabase = supabase;
        _db = db;
        _connectivity = connectivity;

        // Monitor connectivity changes for automatic sync — wrapped to prevent constructor crash
        try
        {
            _connectivity.MonitorConnectivity(async (isConnected) =>
            {
                try
                {
                    if (isConnected && _supabase.IsAuthenticated)
                    {
                        Debug.WriteLine("SyncService: Connection restored, starting background sync");
                        await SyncPendingUploadsAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SyncService: Background sync failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SyncService: Failed to set up connectivity monitoring: {ex.Message}");
        }
    }

    public Task UpdateLocalThumbnailAsync(string memoryId, string? path) =>
        _db.UpdateLocalThumbnailAsync(memoryId, path);

    /// <summary>
    /// Uploads a locally-generated thumbnail to Supabase Storage and records it on
    /// momo_memories.thumbnail_url so the preview propagates to other devices.
    /// Non-fatal: skips silently when offline/unauthenticated or already uploaded,
    /// and never throws so it is safe to fire-and-forget off a page load.
    /// </summary>
    public async Task TryUploadThumbnailAsync(Memory m, string thumbPath, CancellationToken ct = default)
    {
        if (!_connectivity.IsConnected) return;
        if (!_supabase.IsAuthenticated) return;
        if (!m.IsSynced) return;                          // proactive: don't upload thumbnails for local-only memories
        if (_missingRemoteRow.ContainsKey(m.Id)) return;  // reactive memo: id matched no remote row earlier this session
        if (!string.IsNullOrEmpty(m.ThumbnailUrl)) return;  // backstop against duplicate upload
        if (string.IsNullOrEmpty(thumbPath) || !File.Exists(thumbPath)) return;
        try
        {
            var url = await _supabase.UploadThumbnailAsync(m.UserId, thumbPath, ct);
            var matched = await _supabase.UpdateMemoryThumbnailAsync(m.Id, url, ct);
            if (matched)
            {
                m.ThumbnailUrl = url;
            }
            else
            {
                // The id matched no remote row — record it so we don't re-upload an orphan thumbnail next pass.
                _missingRemoteRow[m.Id] = 1;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SyncService: thumbnail upload failed (non-fatal): {ex.Message}");
        }
    }

    public async Task<List<Tag>> GetTagsWithCacheAsync(string? userId, bool includeInactive = false, CancellationToken ct = default)
    {
        try
        {
            if (_connectivity.IsConnected)
            {
                var tags = await _supabase.GetTagsAsync(userId, includeInactive, ct);
                if (tags.Count > 0) await _db.UpsertTagsAsync(tags);
                return tags;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SyncService: Failed to fetch tags from Supabase: {ex.Message}");
        }

        // Fallback to local cache
        return await _db.GetTagsAsync(userId, includeInactive);
    }

    public async Task<List<Memory>> GetMemoriesWithCacheAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            if (_connectivity.IsConnected)
            {
                var remote = await _supabase.GetMemoriesAsync(userId, ct);
                if (remote.Count > 0) await _db.UpsertMemoriesAsync(remote);

                // Merge LocalVideoPath / LocalThumbnailPath from local DB back into the
                // authoritative Supabase list so thumbnail backfill can find local video files.
                var localRows  = await _db.GetMemoriesAsync(userId);
                var localById  = localRows.ToDictionary(m => m.Id);
                foreach (var m in remote)
                {
                    m.IsSynced = true;   // came from Supabase → has a remote row to PATCH
                    if (localById.TryGetValue(m.Id, out var local))
                    {
                        m.LocalVideoPath      = local.LocalVideoPath;
                        m.LocalThumbnailPath  = local.LocalThumbnailPath;
                    }
                }

                // Include locally-pending uploads not yet confirmed in Supabase
                var remoteIds  = remote.Select(m => m.Id).ToHashSet();
                var pendingIds = (await _db.GetPendingUploadMemoriesAsync()).Select(m => m.Id).ToHashSet();
                var pending    = localRows
                    .Where(m => pendingIds.Contains(m.Id) && !remoteIds.Contains(m.Id))
                    .ToList();

                return remote.Concat(pending).OrderByDescending(m => m.CreatedAt).ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SyncService: GetMemoriesWithCacheAsync failed, falling back to local: {ex.Message}");
        }

        // Offline fallback — local DB is authoritative
        return await _db.GetMemoriesAsync(userId);
    }

    /// <summary>
    /// Save memory locally and queue for upload when online.
    /// </summary>
    public async Task<SaveMemoryResult> SaveMemoryAsync(Memory memory, string? localVideoPath = null, CancellationToken ct = default)
    {
        var localMemoryId = memory.Id;

        // Always save to local database first
        await _db.UpsertMemoryAsync(memory);

        // If online and authenticated, upload immediately
        if (_connectivity.IsConnected && _supabase.IsAuthenticated)
        {
            try
            {
                // Upload video if we have a local path
                if (!string.IsNullOrEmpty(localVideoPath) && File.Exists(localVideoPath))
                {
                    var size = new FileInfo(localVideoPath).Length;
                    Diagnostics.Trace(UploadOperation, $"uploading {size:N0} bytes from {localVideoPath}");
                    var videoUrl = await _supabase.UploadVideoAsync(memory.UserId, localVideoPath, "video/mp4", ct);
                    memory.VideoUrl = videoUrl;
                }

                // Insert/update memory in Supabase
                var remoteMemory = await _supabase.InsertMemoryAsync(memory, ct);

                // Update local with remote ID and URL
                memory.Id = remoteMemory.Id;
                memory.VideoUrl = remoteMemory.VideoUrl;
                memory.IsSynced = true;   // remote row now exists → thumbnail upload may PATCH it
                await _db.UpsertMemoryAsync(memory);
                await DeleteLocalPlaceholderAsync(localMemoryId, memory.Id);

                Diagnostics.Trace(UploadOperation, $"memory synced: {memory.Id}");
                return new SaveMemoryResult(remoteMemory, UploadedToCloud: true, WasOffline: false, UploadError: null);
            }
            catch (Exception ex)
            {
                // This catch used to make a failed upload indistinguishable from being offline,
                // and the UI then told the user everything was fine. Report it and say so.
                Diagnostics.Report(ex, UploadOperation, new Dictionary<string, string>
                {
                    ["memoryId"]  = localMemoryId,
                    ["localPath"] = localVideoPath ?? "(none)",
                    ["sizeBytes"] = localVideoPath is not null && File.Exists(localVideoPath)
                        ? new FileInfo(localVideoPath).Length.ToString()
                        : "0",
                });

                // Mark as pending upload
                await _db.MarkMemoryAsPendingUploadAsync(localMemoryId, localVideoPath);
                return new SaveMemoryResult(memory, UploadedToCloud: false, WasOffline: false, UploadError: ex);
            }
        }

        Diagnostics.Trace(UploadOperation, "offline — saved locally, will sync when online");
        // Mark as pending upload
        await _db.MarkMemoryAsPendingUploadAsync(localMemoryId, localVideoPath);
        return new SaveMemoryResult(memory, UploadedToCloud: false, WasOffline: true, UploadError: null);
    }

    private const string UploadOperation = "sync.upload";

    /// <summary>
    /// Sync all pending uploads to Supabase.
    /// </summary>
    public async Task<SyncResult> SyncPendingUploadsAsync(bool wifiOnlyMode = false, CancellationToken ct = default)
    {
        if (_isSyncing)
        {
            Debug.WriteLine("SyncService: Sync already in progress");
            return new SyncResult { Success = false, Message = "Sync already in progress" };
        }

        if (!_connectivity.ShouldUpload(wifiOnlyMode))
        {
            var reason = wifiOnlyMode ? "WiFi-only mode enabled and not on WiFi" : "No internet connection";
            Debug.WriteLine($"SyncService: Cannot sync - {reason}");
            return new SyncResult { Success = false, Message = reason };
        }

        if (!_supabase.IsAuthenticated)
        {
            Debug.WriteLine("SyncService: Cannot sync - not authenticated");
            return new SyncResult { Success = false, Message = "Not authenticated" };
        }

        _isSyncing = true;
        var result = new SyncResult();

        try
        {
            Debug.WriteLine("SyncService: Starting pending uploads sync");

            // Get all memories pending upload
            var pendingMemories = await _db.GetPendingUploadMemoriesAsync();
            Debug.WriteLine($"SyncService: Found {pendingMemories.Count} pending uploads");

            foreach (var memory in pendingMemories)
            {
                var localMemoryId = memory.Id;

                try
                {
                    // Get the local video path
                    var localVideoPath = await _db.GetPendingUploadPathAsync(localMemoryId);

                    // Upload video if we have a local file
                    if (!string.IsNullOrEmpty(localVideoPath) && File.Exists(localVideoPath))
                    {
                        Debug.WriteLine($"SyncService: Uploading video: {localVideoPath}");
                        var videoUrl = await _supabase.UploadVideoAsync(memory.UserId, localVideoPath, "video/mp4", ct);
                        memory.VideoUrl = videoUrl;
                    }

                    // Insert/update in Supabase
                    var remoteMemory = await _supabase.InsertMemoryAsync(memory, ct);

                    // Update local with remote data
                    memory.Id = remoteMemory.Id;
                    memory.VideoUrl = remoteMemory.VideoUrl;
                    memory.IsSynced = true;   // remote row now exists → thumbnail upload may PATCH it
                    await _db.UpsertMemoryAsync(memory);
                    await DeleteLocalPlaceholderAsync(localMemoryId, memory.Id);

                    // Clear pending upload flag
                    await _db.ClearPendingUploadAsync(localMemoryId);

                    // A thumbnail generated at offline-capture time (LocalThumbnailPath) never
                    // got uploaded because IsSynced wasn't true yet — do it now that the remote
                    // row exists, so this memory shows its real thumbnail on other devices too
                    // instead of falling back to the gradient placeholder.
                    if (!string.IsNullOrEmpty(memory.LocalThumbnailPath))
                        await TryUploadThumbnailAsync(memory, memory.LocalThumbnailPath, ct);

                    result.UploadedCount++;
                    Debug.WriteLine($"SyncService: Successfully synced memory: {memory.Id}");
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    await _db.IncrementRetryCountAsync(localMemoryId);
                    Debug.WriteLine($"SyncService: Failed to sync memory {localMemoryId}: {ex.Message}");
                    // Keep it in pending queue for next sync attempt
                }
            }

            result.Success = result.FailedCount == 0;
            result.Message = result.Success 
                ? $"Successfully synced {result.UploadedCount} memories" 
                : $"Synced {result.UploadedCount}, failed {result.FailedCount}";

            Debug.WriteLine($"SyncService: Sync completed - {result.Message}");
            SyncCompleted?.Invoke(this, new SyncEventArgs(result));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Sync failed: {ex.Message}";
            Debug.WriteLine($"SyncService: Sync failed with exception: {ex}");
            SyncFailed?.Invoke(this, new SyncEventArgs(result));
        }
        finally
        {
            _isSyncing = false;
        }

        return result;
    }

    private Task DeleteLocalPlaceholderAsync(string localMemoryId, string remoteMemoryId)
    {
        if (string.Equals(localMemoryId, remoteMemoryId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return _db.DeleteMemoryAsync(localMemoryId);
    }

    /// <summary>
    /// Force a full sync of all data from Supabase.
    /// </summary>
    public async Task<SyncResult> FullSyncAsync(string userId, CancellationToken ct = default)
    {
        if (!_connectivity.IsConnected || !_supabase.IsAuthenticated)
        {
            return new SyncResult 
            { 
                Success = false, 
                Message = "Cannot perform full sync - offline or not authenticated" 
            };
        }

        var result = new SyncResult();

        try
        {
            Debug.WriteLine("SyncService: Starting full sync");

            // Sync tags
            var tags = await _supabase.GetTagsAsync(userId, includeInactive: false, ct);
            await _db.UpsertTagsAsync(tags);
            result.DownloadedCount += tags.Count;

            // Sync memories
            var memories = await _supabase.GetMemoriesAsync(userId, ct);
            await _db.UpsertMemoriesAsync(memories);
            result.DownloadedCount += memories.Count;

            result.Success = true;
            result.Message = $"Downloaded {result.DownloadedCount} items";
            Debug.WriteLine($"SyncService: Full sync completed - {result.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Full sync failed: {ex.Message}";
            Debug.WriteLine($"SyncService: Full sync failed: {ex}");
        }

        return result;
    }
}

/// <summary>
/// Outcome of a save. Lets the caller tell "saved locally because we're offline" apart from
/// "the upload failed while online" — the UI used to report both as a cheerful success.
/// </summary>
/// <param name="Memory">The saved memory, carrying the remote id and URL when the upload succeeded.</param>
/// <param name="UploadedToCloud">True only when the video and row both reached Supabase.</param>
/// <param name="WasOffline">True when no upload was attempted because the device had no connection.</param>
/// <param name="UploadError">The failure, when an upload was attempted and did not succeed.</param>
public sealed record SaveMemoryResult(
    Memory Memory,
    bool UploadedToCloud,
    bool WasOffline,
    Exception? UploadError);

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UploadedCount { get; set; }
    public int DownloadedCount { get; set; }
    public int FailedCount { get; set; }
}

public class SyncEventArgs : EventArgs
{
    public SyncResult Result { get; }

    public SyncEventArgs(SyncResult result)
    {
        Result = result;
    }
}
