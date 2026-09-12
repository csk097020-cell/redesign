namespace MomentaryMomentos.Services;

/// <summary>
/// Manages local file storage for videos and images.
/// Files are saved to app local storage until they can be synced to Supabase.
/// </summary>
public class LocalStorageService
{
    private readonly string _videosPath;
    private readonly string _thumbnailsPath;
    private readonly string _tempPath;

    public LocalStorageService()
    {
        // Create directories in app's local storage
        _videosPath = Path.Combine(FileSystem.AppDataDirectory, "videos");
        _thumbnailsPath = Path.Combine(FileSystem.AppDataDirectory, "thumbnails");
        _tempPath = Path.Combine(FileSystem.CacheDirectory, "temp");

        Directory.CreateDirectory(_videosPath);
        Directory.CreateDirectory(_thumbnailsPath);
        Directory.CreateDirectory(_tempPath);
    }

    /// <summary>
    /// Save a video file locally and return the local path.
    /// </summary>
    public async Task<string> SaveVideoAsync(string fileName, Stream videoStream)
    {
        var localPath = Path.Combine(_videosPath, fileName);

        try
        {
            using var fileStream = File.Create(localPath);
            await videoStream.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save video '{fileName}' to local storage.", ex);
        }

        return localPath;
    }

    /// <summary>
    /// Subtrees inside the cache directory that belong to other components and must survive a
    /// sweep. Sentry's envelope and native-crash state are pending uploads — deleting them loses
    /// the crash report we are trying to receive — and Glide's disk cache is already self-bounded.
    /// </summary>
    private static readonly string[] CacheSweepExclusions =
    {
        "Sentry", "android", "image_manager_disk_cache",
    };

    /// <summary>
    /// Deletes stale intermediates from the whole cache tree.
    ///
    /// Build 9's version only enumerated files sitting *directly* in the cache root and only
    /// matched <c>trim_</c>/<c>cmp_</c>/<c>thumb_</c> — it reported reclaiming 42 MB while 1.9 GB
    /// of media-picker copies sat two directories below it, untouched. Sweeping the tree by age
    /// instead of by name also covers <c>avatar_*</c>, <c>momo_*</c> downloads, and whatever the
    /// next component decides to drop in here.
    ///
    /// Safe by construction: the OS may purge a cache directory at any moment, so nothing is
    /// allowed to depend on its contents surviving. The videos folder is a different directory
    /// and is deliberately never touched — those files back memory rows, including pending uploads.
    /// </summary>
    public void CleanTemporaryFiles(TimeSpan olderThan)
    {
        var cutoff  = DateTime.UtcNow - olderThan;
        var removed = 0;
        long bytes  = 0;

        try
        {
            var root = new DirectoryInfo(FileSystem.CacheDirectory);

            foreach (var child in root.EnumerateDirectories())
            {
                if (CacheSweepExclusions.Contains(child.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                SweepDirectory(child, cutoff, ref removed, ref bytes);
            }

            foreach (var file in root.EnumerateFiles())
                TryDeleteExpired(file, cutoff, ref removed, ref bytes);

            if (removed > 0)
                Diagnostics.Trace("storage.cleanup", $"removed {removed} temp files ({bytes / (1024.0 * 1024.0):F1} MB)");
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "storage.cleanup");
        }
    }

    /// <summary>
    /// Deletes expired files under <paramref name="dir"/>, then removes the directory itself if
    /// nothing is left in it — the picker creates a fresh per-pick folder every time, so leaving
    /// the empty shells behind would trade 1.9 GB of video for thousands of empty directories.
    /// </summary>
    private static void SweepDirectory(DirectoryInfo dir, DateTime cutoff, ref int removed, ref long bytes)
    {
        try
        {
            foreach (var child in dir.EnumerateDirectories())
                SweepDirectory(child, cutoff, ref removed, ref bytes);

            foreach (var file in dir.EnumerateFiles())
                TryDeleteExpired(file, cutoff, ref removed, ref bytes);

            dir.Refresh();
            if (!dir.EnumerateFileSystemInfos().Any())
                dir.Delete();
        }
        catch { /* unreadable or in use — the next sweep tries again */ }
    }

    private static void TryDeleteExpired(FileInfo file, DateTime cutoff, ref int removed, ref long bytes)
    {
        try
        {
            if (file.LastWriteTimeUtc > cutoff) return;

            var size = file.Length;
            file.Delete();
            removed++;
            bytes += size;
        }
        catch { /* a file in use will be caught on the next sweep */ }
    }

    /// <summary>
    /// Deletes the copy the media picker made of a picked file, once our own copy exists.
    ///
    /// MAUI Essentials copies every picked file into <c>CacheDirectory/&lt;hash&gt;/&lt;hash&gt;/&lt;name&gt;</c>
    /// and never removes it. Every video pick leaked ~80 MB permanently that way; one test device
    /// held 29 full copies at 1.9 GB.
    ///
    /// Deleting <c>FileResult.FullPath</c> does not work on Android, which is why this does more
    /// than that. Measured on device, the picker returns the copy addressed as
    /// <c>/data/data/&lt;pkg&gt;/cache/…</c> while <c>FileSystem.CacheDirectory</c> reports
    /// <c>/data/user/0/&lt;pkg&gt;/cache</c>. Those are the same storage, but no string comparison
    /// says so, and the containment check has to stay: the alternative is a blind delete of a path
    /// that on some platform or provider is the user's own file in shared storage.
    ///
    /// So the copy is reclaimed by clearing the picker's staging root instead, which is safe
    /// because our own copy is already written by this point and picks are strictly serial.
    ///
    /// Never touches a path outside the cache directory. Failures are swallowed —
    /// <see cref="CleanTemporaryFiles"/> is the backstop.
    /// </summary>
    public void DiscardPickerTempFile(string? fullPath)
    {
        try
        {
            var cacheRoot = Path.GetFullPath(FileSystem.CacheDirectory);

            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                var target = Path.GetFullPath(fullPath);

                // Only when the path really is inside our cache. Anything else is the user's own
                // file — that is the whole reason this is guarded rather than a blind delete.
                if (target.StartsWith(cacheRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var size = new FileInfo(target).Length;
                    File.Delete(target);

                    // The picker makes a throwaway directory per pick; drop it if this emptied it.
                    var parent = new DirectoryInfo(Path.GetDirectoryName(target)!);
                    if (parent.FullName.Length > cacheRoot.Length && !parent.EnumerateFileSystemInfos().Any())
                        parent.Delete();

                    Diagnostics.Trace(PickerDiscardOperation, $"deleted by path ({size / (1024.0 * 1024.0):F1} MB)");
                }
                else
                {
                    // Expected on Android (root=/data/data) — the staging-root clear below is what
                    // reclaims it. Root segments only, never the filename: traces become Sentry
                    // breadcrumbs, and this path may be the user's own file on another platform.
                    var root = string.Join("/", target.Split(Path.DirectorySeparatorChar).Take(3));
                    Diagnostics.Trace(PickerDiscardOperation, $"path not under cache root (root={root})");
                }
            }

            // Always run: on Android this is the branch that actually reclaims the leak.
            ClearPickerCacheRoots(cacheRoot);
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, PickerDiscardOperation);
        }
    }

    private const string PickerDiscardOperation = "storage.picker-discard";

    /// <summary>
    /// Empties the staging directories MAUI's picker copies into. They are named with a 32-character
    /// hex hash directly under the cache root, which distinguishes them from every other cache
    /// tenant (Sentry, android, image_manager_disk_cache) without hardcoding the hash — it is a
    /// MAUI implementation detail that could change between releases.
    /// </summary>
    private static void ClearPickerCacheRoots(string cacheRoot)
    {
        var removed = 0;
        long bytes  = 0;

        foreach (var dir in new DirectoryInfo(cacheRoot).EnumerateDirectories())
        {
            if (!IsHashName(dir.Name)) continue;

            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    var size = file.Length;
                    file.Delete();
                    removed++;
                    bytes += size;
                }
                catch { /* in use — the startup sweep retries */ }
            }

            foreach (var child in dir.EnumerateDirectories())
            {
                try { if (!child.EnumerateFileSystemInfos().Any()) child.Delete(true); }
                catch { /* ditto */ }
            }
        }

        if (removed > 0)
            Diagnostics.Trace(PickerDiscardOperation, $"cleared {removed} picker copies ({bytes / (1024.0 * 1024.0):F1} MB)");
        else
            Diagnostics.Trace(PickerDiscardOperation, "nothing to clear");
    }

    private static bool IsHashName(string name) =>
        name.Length == 32 && name.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

    /// <summary>
    /// Moves a video into permanent app storage and returns its new path.
    ///
    /// The trim and compress steps write to the cache directory, which Android is free to purge
    /// under storage pressure. A memento saved offline sits in the pending-upload queue holding
    /// only a path, so a purged cache file means the video is simply gone by the time the upload
    /// retries. Anything destined for upload has to leave the cache first.
    ///
    /// Returns the original path unchanged if the move fails — a cache file we still have beats
    /// no file at all.
    /// </summary>
    public string MoveIntoVideos(string sourcePath)
    {
        try
        {
            if (!File.Exists(sourcePath)) return sourcePath;

            // Already permanent — nothing to do.
            if (Path.GetDirectoryName(sourcePath)?.Equals(_videosPath, StringComparison.OrdinalIgnoreCase) == true)
                return sourcePath;

            var destination = Path.Combine(_videosPath, Path.GetFileName(sourcePath));
            if (File.Exists(destination)) File.Delete(destination);

            File.Move(sourcePath, destination);
            return destination;
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "storage.persist-video", new Dictionary<string, string>
            {
                ["sourcePath"] = sourcePath,
            });
            return sourcePath;
        }
    }

    /// <summary>
    /// Save a thumbnail/image locally and return the local path.
    /// </summary>
    public async Task<string> SaveThumbnailAsync(string fileName, Stream imageStream)
    {
        var localPath = Path.Combine(_thumbnailsPath, fileName);

        try
        {
            using var fileStream = File.Create(localPath);
            await imageStream.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save thumbnail '{fileName}' to local storage.", ex);
        }

        return localPath;
    }

    /// <summary>
    /// Get a video file stream from local storage.
    /// </summary>
    public FileStream GetVideoStream(string fileName)
    {
        var localPath = Path.Combine(_videosPath, fileName);
        
        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Video not found: {fileName}");
        
        return File.OpenRead(localPath);
    }

    /// <summary>
    /// Get a thumbnail file stream from local storage.
    /// </summary>
    public FileStream GetThumbnailStream(string fileName)
    {
        var localPath = Path.Combine(_thumbnailsPath, fileName);
        
        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Thumbnail not found: {fileName}");
        
        return File.OpenRead(localPath);
    }

    /// <summary>
    /// Check if a video exists locally.
    /// </summary>
    public bool VideoExists(string fileName)
    {
        var localPath = Path.Combine(_videosPath, fileName);
        return File.Exists(localPath);
    }

    /// <summary>
    /// Get all videos that haven't been uploaded yet.
    /// </summary>
    public List<string> GetPendingVideos()
    {
        return Directory.GetFiles(_videosPath)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// Delete a video file from local storage (after successful upload).
    /// </summary>
    public void DeleteVideo(string fileName)
    {
        var localPath = Path.Combine(_videosPath, fileName);
        if (!File.Exists(localPath)) return;

        try
        {
            File.Delete(localPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteVideo: Failed to delete '{fileName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Get the full local path for a video.
    /// </summary>
    public string GetVideoPath(string fileName)
    {
        return Path.Combine(_videosPath, fileName);
    }

    /// <summary>
    /// Get the full local path for a thumbnail.
    /// </summary>
    public string GetThumbnailPath(string fileName)
    {
        return Path.Combine(_thumbnailsPath, fileName);
    }

    /// <summary>
    /// Get total size of local videos in bytes.
    /// </summary>
    public long GetTotalVideoSize()
    {
        var files = Directory.GetFiles(_videosPath);
        return files.Sum(f => new FileInfo(f).Length);
    }

    /// <summary>
    /// Clear all local videos (use with caution!).
    /// </summary>
    public void ClearAllVideos()
    {
        foreach (var file in Directory.GetFiles(_videosPath))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearAllVideos: Failed to delete '{file}': {ex.Message}");
            }
        }
    }
}
