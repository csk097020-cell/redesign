namespace MomentaryMomentos.Services;

/// <summary>
/// Reads video metadata and trims a video clip to an exact 10-second window.
/// Platform-specific implementations live in Platforms/Android and Platforms/iOS.
/// </summary>
public interface IVideoTrimService
{
    /// <summary>Returns the duration of the video at the given local file path.</summary>
    Task<TimeSpan> GetDurationAsync(string videoPath);

    /// <summary>
    /// Extracts a <paramref name="trimDuration"/> window starting at <paramref name="startTime"/>
    /// from <paramref name="inputPath"/> and writes the result to a new temp file.
    /// Returns the output path on success, null on failure.
    /// Progress is reported as 0.0–1.0 if a reporter is supplied.
    /// </summary>
    Task<string?> TrimVideoAsync(
        string inputPath,
        TimeSpan startTime,
        TimeSpan trimDuration,
        IProgress<double>? progress = null);

    /// <summary>
    /// Re-encodes <paramref name="inputPath"/> at a delivery-grade bitrate, writing the result to
    /// a new temp file.
    ///
    /// A 10-second 4K clip off a modern phone is ~85 MB, which busts the 50 MB upload ceiling and
    /// costs roughly 7x the storage it needs to. Trimming cannot help — the clip is already short
    /// — because phone cameras record at wasteful editing bitrates (~72 Mbps for 4K).
    ///
    /// How much each platform changes is deliberately left to the implementation: iOS downscales
    /// to 1080p via an export preset, while Android keeps the source resolution and cuts bitrate
    /// only, because surface-transcode scaling is rejected outright by some hardware encoders.
    ///
    /// Returns the output path on success, or null when compression was unavailable, unnecessary,
    /// or failed — callers must fall back to judging the original file. Progress is 0.0–1.0.
    /// </summary>
    Task<string?> CompressVideoAsync(
        string inputPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts the first frame of the video as a JPEG and writes it to a temp file.
    /// Returns the local file path on success, null on failure.
    /// </summary>
    Task<string?> GetThumbnailAsync(string videoPath, CancellationToken ct = default);

    /// <summary>
    /// Returns the original creation date embedded in the video file metadata.
    /// Falls back to the file OS creation time, then null if neither is available.
    /// </summary>
    Task<DateTimeOffset?> GetCreationDateAsync(string videoPath);
}
