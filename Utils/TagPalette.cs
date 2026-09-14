namespace MomentaryMomentos.Utils;

/// <summary>
/// Curated set of tag colors. Tags no longer let the user pick a color — one is
/// assigned automatically on creation from this palette. Colors match the warm
/// keepsake redesign's cream/tan/olive world (four are the app's own brand
/// accents) rather than generic saturated primaries, so any tag chip belongs
/// next to the rest of the app instead of looking like a leftover default.
/// </summary>
public static class TagPalette
{
    public static readonly IReadOnlyList<string> Colors =
    [
        "#D84C9A", // rose (brand Accent)
        "#C2603D", // terracotta
        "#C99A3B", // gold / mustard
        "#9CA05A", // warm olive-yellow
        "#788760", // olive / sage (brand GlowGreen)
        "#5E9080", // sage teal
        "#49B9D3", // cyan (brand GlowCyan)
        "#6E7FB0", // dusty blue
        "#8B63C9", // violet (brand GlowViolet)
        "#9C5F82", // mauve / plum
        "#A5473A", // brick red
        "#D97862", // soft coral
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
