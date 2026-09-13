namespace MomentaryMomentos.Models;

/// <summary>
/// Lightweight display-only tag info resolved from a tag ID.
/// Populated by ViewModels after looking up the tag in the loaded tag list.
/// </summary>
public sealed record TagInfo(string Name, string Color, string Icon)
{
    // Tags display name-only now; Icon is retained for data round-trip but never shown.
    public string DisplayLabel => Name;
}
