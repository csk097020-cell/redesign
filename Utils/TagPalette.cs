namespace MomentaryMomentos.Utils;

/// <summary>
/// Curated set of tag colors. Tags no longer let the user pick a color — one is
/// assigned automatically on creation from this palette. Colors are vivid,
/// high-contrast-on-dark (Tailwind-500 family), deliberately avoiding muddy /
/// low-saturation tones so any combination looks good together.
/// </summary>
public static class TagPalette
{
    public static readonly IReadOnlyList<string> Colors =
    [
        "#EF4444", // red
        "#F97316", // orange
        "#F59E0B", // amber
        "#EAB308", // yellow
        "#84CC16", // lime
        "#22C55E", // green
        "#14B8A6", // teal
        "#06B6D4", // cyan
        "#3B82F6", // blue
        "#6366F1", // indigo
        "#8B5CF6", // violet
        "#EC4899", // pink
    ];

    /// <summary>
    /// Picks the next color for a new tag. Cycles through the palette based on how
    /// many tags already exist, which advances deterministically and avoids
    /// repeating the previous tag's color on consecutive creations.
    /// </summary>
    public static string NextColor(IEnumerable<string> existingColors)
    {
        var count = existingColors?.Count() ?? 0;
        return Colors[count % Colors.Count];
    }
}
