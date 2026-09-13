using AVFoundation;
using CoreMedia;
using Foundation;
using MomentaryMomentos.Services;
using UIKit;

namespace MomentaryMomentos.Services;

/// <summary>
/// iOS video trimmer using AVAssetExportSession.
/// Apple handles all seek/sync frame logic internally so the output
/// always starts on a clean frame boundary.
/// </summary>
public class VideoTrimService : IVideoTrimService
{
    // ── Duration ────────────────────────────────────────────────────────────
    public Task<TimeSpan> GetDurationAsync(string videoPath)
    {
        return Task.Run<TimeSpan>(() =>
        {
            try
            {
                var url   = NSUrl.FromFilename(videoPath);
                var asset = AVAsset.FromUrl(url);
                return TimeSpan.FromSeconds(asset.Duration.Seconds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoTrimService] GetDuration error: {ex.Message}");
                return TimeSpan.Zero;
            }
        });
    }

    // ── Creation date ────────────────────────────────────────────────────────
    public Task<DateTimeOffset?> GetCreationDateAsync(string videoPath)
    {
        return Task.Run<DateTimeOffset?>(() =>
        {
            try
            {
                var url   = NSUrl.FromFilename(videoPath);
                var asset = AVAsset.FromUrl(url);
                var items = asset.CommonMetadata;
                foreach (var item in items)
                {
                    if (item.CommonKey?.ToString() == "creationDate"
                        && item.Value is NSDate nsDate)
                    {
                        var epoch   = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var seconds = nsDate.SecondsSinceReferenceDate;
                        return new DateTimeOffset(epoch.AddSeconds(seconds), TimeSpan.Zero);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoTrimService] GetCreationDate metadata error: {ex.Message}");
            }

            try
            {
                var fileTime = File.GetCreationTimeUtc(videoPath);
                if (fileTime > DateTime.UnixEpoch)
                    return new DateTimeOffset(fileTime, TimeSpan.Zero);
            }
            catch { }

            return null;
        });
    }

        // ── Thumbnail ────────────────────────────────────────────────────────────
    public Task<string?> GetThumbnailAsync(string videoPath, CancellationToken ct = default)
    {
        return Task.Run<string?>(() =>
        {
            try
            {
                var url   = NSUrl.FromFilename(videoPath);
                var asset = AVAsset.FromUrl(url);

                using var generator = new AVAssetImageGenerator(asset);
                generator.AppliesPreferredTrackTransform = true;

                var cgImage = generator.CopyCGImageAtTime(CMTime.Zero, out _, out var error);
                if (cgImage is null || error is not null) return null;

                var uiImage  = new UIImage(cgImage);
                var jpegData = uiImage.AsJPEG(0.8f);
                if (jpegData is null) return null;

                var outputPath = System.IO.Path.Combine(FileSystem.CacheDirectory, $"thumb_{Guid.NewGuid():N}.jpg");
                jpegData.Save(outputPath, atomically: true);
                return System.IO.File.Exists(outputPath) ? outputPath : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoTrimService] GetThumbnail error: {ex.Message}");
                return null;
            }
        }, ct);
    }

    // ── Compress ─────────────────────────────────────────────────────────────
    /// <summary>
    /// AVAssetExportSession does the whole job from a preset — it downscales to 1080p, re-encodes
    /// and caps the bitrate — so there is no hand-rolled codec pipeline here the way there is on
    /// Android, and iOS can afford to reduce resolution as well as bitrate.
    /// </summary>
    public async Task<string?> CompressVideoAsync(
        string inputPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var outputPath = Path.Combine(FileSystem.CacheDirectory, $"cmp_{Guid.NewGuid():N}.mp4");

        try
        {
            var url   = NSUrl.FromFilename(inputPath);
            var asset = AVAsset.FromUrl(url);

            // A preset the source can't satisfy yields a null session rather than an error.
            using var export = AVAssetExportSession.FromAsset(asset, AVAssetExportSession.Preset1920x1080);
            if (export is null) return null;

            export.OutputUrl                   = NSUrl.FromFilename(outputPath);
            export.OutputFileType              = AVFileTypes.Mpeg4.GetConstant()!;
            export.ShouldOptimizeForNetworkUse = true;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (progress != null)
            {
                _ = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        progress.Report(export.Progress);
                        await Task.Delay(80, cts.Token).ConfigureAwait(false);
                    }
                }, cts.Token);
            }

            await export.ExportTaskAsync().ConfigureAwait(false);
            cts.Cancel();

            if (export.Status == AVAssetExportSessionStatus.Completed && File.Exists(outputPath))
            {
                progress?.Report(1.0);
                return outputPath;
            }

            Diagnostics.Trace("video.compress",
                $"export failed: {export.Status}, {export.Error?.LocalizedDescription}");
            return null;
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "video.compress", new Dictionary<string, string> { ["inputPath"] = inputPath });
            return null;
        }
    }

    // ── Trim ─────────────────────────────────────────────────────────────────
    public async Task<string?> TrimVideoAsync(
        string inputPath,
        TimeSpan startTime,
        TimeSpan trimDuration,
        IProgress<double>? progress = null)
    {
        var outputPath = Path.Combine(FileSystem.CacheDirectory, $"trim_{Guid.NewGuid():N}.mp4");

        try
        {
            var url   = NSUrl.FromFilename(inputPath);
            var asset = AVAsset.FromUrl(url);

            using var export = AVAssetExportSession.FromAsset(asset, AVAssetExportSession.PresetHighestQuality);
            if (export is null) return null;

            export.OutputUrl              = NSUrl.FromFilename(outputPath);
            export.OutputFileType         = AVFileTypes.Mpeg4.GetConstant()!;
            export.ShouldOptimizeForNetworkUse = true;

            export.TimeRange = new CMTimeRange
            {
                Start    = CMTime.FromSeconds(startTime.TotalSeconds, 600),
                Duration = CMTime.FromSeconds(trimDuration.TotalSeconds, 600)
            };

            // Poll export progress on a background task
            using var cts = new CancellationTokenSource();
            if (progress != null)
            {
                _ = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        progress.Report(export.Progress);
                        await Task.Delay(80, cts.Token).ConfigureAwait(false);
                    }
                }, cts.Token);
            }

            await export.ExportTaskAsync().ConfigureAwait(false);
            cts.Cancel();

            if (export.Status == AVAssetExportSessionStatus.Completed && File.Exists(outputPath))
            {
                progress?.Report(1.0);
                return outputPath;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[VideoTrimService] Export failed: {export.Status}, {export.Error?.LocalizedDescription}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VideoTrimService] Trim error: {ex.Message}");
            return null;
        }
    }
}
