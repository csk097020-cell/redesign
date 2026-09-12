// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
namespace MomentaryMomentos.Models;

/// <summary>
/// Represents a 10-second video memory captured by the user.
/// Core data entity per contract Appendix A Section 2.
/// </summary>
public sealed class Memory
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    /// <summary>User-facing title. Falls back to an auto-generated "Tag - Date" label when blank.</summary>
    public string Title { get; set; } = "Untitled Memory";
    /// <summary>Optional free-text caption entered by the user at capture.</summary>
    public string? Caption { get; set; }
    public string? VideoUrl { get; set; }
    /// <summary>Supabase Storage URL for the video thumbnail image.</summary>
    public string? ThumbnailUrl { get; set; }
    /// <summary>Local path for offline-captured videos before upload.</summary>
    public string? LocalVideoPath { get; set; }
    /// <summary>Local cache path for the extracted thumbnail JPEG.</summary>
    public string? LocalThumbnailPath { get; set; }

    /// <summary>
    /// Resolved thumbnail source for display: prefers the locally cached frame
    /// when available, falls back to the remote URL.
    /// </summary>
    public string? DisplayThumbnailSource => LocalThumbnailPath ?? ThumbnailUrl;
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Resolved tag display info (name + color + icon).
    /// Populated by ViewModels after loading the tag list — never stored in the DB.
    /// </summary>
    public List<TagInfo> DisplayTags { get; set; } = new();

    public bool IsFavorite { get; set; }
    /// <summary>Automatic upload timestamp, set at save time. Surfaced as "Date Uploaded".</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// CreatedAt in the device's timezone. Bind this, never CreatedAt, anywhere a date or
    /// time is shown: Postgres hands back a UTC timestamptz and DateTimeOffset.Parse keeps
    /// the +00:00 offset, so formatting CreatedAt directly prints the UTC clock. A memory
    /// saved at 8:19 PM CDT is 01:19 UTC the next day, and the card read a day late.
    /// FeaturedClipService already compares on CreatedAt.LocalDateTime; this keeps the
    /// printed date agreeing with it.
    /// </summary>
    public DateTimeOffset CreatedAtLocal => CreatedAt.ToLocalTime();
    /// <summary>
    /// Optional manual date the user says the moment was actually filmed ("Date Captured").
    /// Calendar day only; null for legacy mementos captured before this field existed.
    /// </summary>
    public DateOnly? DateCaptured { get; set; }
    /// <summary>True when synced to Supabase storage.</summary>
    public bool IsSynced { get; set; }
}
