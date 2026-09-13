using Android.Media;
using Java.Nio;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.Services;

/// <summary>
/// Android video trimmer using MediaExtractor + MediaMuxer.
/// No re-encoding — extracts raw compressed samples directly, so it is fast
/// and lossless. Seeks to the nearest sync (key) frame before startTime so
/// the output always starts cleanly.
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
                using var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(videoPath);
                var durationMs = retriever.ExtractMetadata(MetadataKey.Duration);
                if (long.TryParse(durationMs, out var ms))
                    return TimeSpan.FromMilliseconds(ms);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoTrimService] GetDuration error: {ex.Message}");
            }
            return TimeSpan.Zero;
        });
    }

    // ── Creation date ────────────────────────────────────────────────────────
    public Task<DateTimeOffset?> GetCreationDateAsync(string videoPath)
    {
        return Task.Run<DateTimeOffset?>(() =>
        {
            try
            {
                using var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(videoPath);
                var raw = retriever.ExtractMetadata(MetadataKey.Date);
                if (!string.IsNullOrWhiteSpace(raw)
                    && DateTimeOffset.TryParse(raw, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToUniversalTime();
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
                using var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(videoPath);
                using var bitmap = retriever.GetFrameAtTime(0, Option.ClosestSync);
                if (bitmap is null) return null;

                var outputPath = System.IO.Path.Combine(FileSystem.CacheDirectory, $"thumb_{Guid.NewGuid():N}.jpg");
                using var ms = new System.IO.MemoryStream();
                if (!bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 80, ms))
                    return null;

                System.IO.File.WriteAllBytes(outputPath, ms.ToArray());
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
    public Task<string?> CompressVideoAsync(
        string inputPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => VideoCompressor.Compress(inputPath, progress, ct), ct);

    // ── Trim ─────────────────────────────────────────────────────────────────
    public Task<string?> TrimVideoAsync(
        string inputPath,
        TimeSpan startTime,
        TimeSpan trimDuration,
        IProgress<double>? progress = null)
    {
        return Task.Run<string?>(() =>
        {
            MediaExtractor? extractor = null;
            MediaMuxer? muxer = null;
            var outputPath = System.IO.Path.Combine(FileSystem.CacheDirectory, $"trim_{Guid.NewGuid():N}.mp4");

            try
            {
                extractor = new MediaExtractor();
                extractor.SetDataSource(inputPath);

                // ── Discover and map tracks ──────────────────────────────────
                int trackCount = extractor.TrackCount;
                var muxerTrackIndex = new int[trackCount];

                muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);

                for (int i = 0; i < trackCount; i++)
                {
                    var format = extractor.GetTrackFormat(i);
                    var mime   = format.GetString(MediaFormat.KeyMime) ?? string.Empty;

                    if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                        mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                    {
                        extractor.SelectTrack(i);
                        muxerTrackIndex[i] = muxer.AddTrack(format);
                    }
                    else
                    {
                        muxerTrackIndex[i] = -1; // skip
                    }
                }

                // ── Seek to nearest sync frame before startTime ──────────────
                long startUs = (long)(startTime.TotalSeconds * 1_000_000L);
                extractor.SeekTo(startUs, MediaExtractorSeekTo.PreviousSync);

                // ── Extract and mux ──────────────────────────────────────────
                muxer.Start();

                long trimUs        = (long)(trimDuration.TotalSeconds * 1_000_000L);
                long? anchorUs     = null; // timestamp of the very first sample we read
                var  buffer        = ByteBuffer.Allocate(2 * 1024 * 1024); // 2 MB
                var  bufferInfo    = new MediaCodec.BufferInfo();

                while (true)
                {
                    int track = extractor.SampleTrackIndex;
                    if (track < 0) break;                     // EOF
                    if (muxerTrackIndex[track] < 0)           // unmapped track
                    {
                        extractor.Advance();
                        continue;
                    }

                    long sampleUs = extractor.SampleTime;

                    // Anchor the first sample so output timestamps start at 0
                    anchorUs ??= sampleUs;

                    long relativeUs = sampleUs - anchorUs.Value;
                    if (relativeUs > trimUs) break;           // past the clip window

                    buffer.Clear();
                    bufferInfo.Offset = 0;
                    bufferInfo.Size   = extractor.ReadSampleData(buffer, 0);
                    if (bufferInfo.Size < 0) break;

                    bufferInfo.PresentationTimeUs = relativeUs;
                    bufferInfo.Flags = (MediaCodecBufferFlags)extractor.SampleFlags;

                    muxer.WriteSampleData(muxerTrackIndex[track], buffer, bufferInfo);
                    extractor.Advance();

                    progress?.Report(Math.Min(1.0, (double)relativeUs / trimUs));
                }

                progress?.Report(1.0);
                muxer.Stop();

                return System.IO.File.Exists(outputPath) ? outputPath : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoTrimService] Trim error: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
            finally
            {
                try { muxer?.Release(); } catch { /* ignored */ }
                try { extractor?.Release(); } catch { /* ignored */ }
            }
        });
    }
}
