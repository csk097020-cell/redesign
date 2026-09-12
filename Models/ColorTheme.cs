namespace MomentaryMomentos.Models;

/// <summary>
/// Defines a color theme associated with tags/emotions.
/// </summary>
public class ColorTheme
{
    public string Name { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#0EA5E9";
    public string SecondaryColor { get; set; } = "#06B6D4";
    public string AccentColor { get; set; } = "#14B8A6";
    public string GradientStart { get; set; } = "#0891B2";
    public string GradientMid { get; set; } = "#06B6D4";
    public string GradientEnd { get; set; } = "#22D3EE";
    
    // Predefined themes based on emotions/moods
    public static ColorTheme Happy => new()
    {
        Name = "Happy",
        PrimaryColor = "#FCD34D",      // Bright Yellow
        SecondaryColor = "#FBBF24",    // Gold
        AccentColor = "#F59E0B",       // Amber
        GradientStart = "#FCD34D",
        GradientMid = "#FBBF24",
        GradientEnd = "#F59E0B"
    };
    
    public static ColorTheme Excited => new()
    {
        Name = "Excited",
        PrimaryColor = "#FB923C",      // Orange
        SecondaryColor = "#F97316",    // Bright Orange
        AccentColor = "#EA580C",       // Dark Orange
        GradientStart = "#FB923C",
        GradientMid = "#F97316",
        GradientEnd = "#EA580C"
    };
    
    public static ColorTheme Love => new()
    {
        Name = "Love",
        PrimaryColor = "#F472B6",      // Pink
        SecondaryColor = "#EC4899",    // Hot Pink
        AccentColor = "#DB2777",       // Deep Pink
        GradientStart = "#F472B6",
        GradientMid = "#EC4899",
        GradientEnd = "#DB2777"
    };
    
    public static ColorTheme Calm => new()
    {
        Name = "Calm",
        PrimaryColor = "#38BDF8",      // Sky Blue
        SecondaryColor = "#0EA5E9",    // Light Blue
        AccentColor = "#0284C7",       // Blue
        GradientStart = "#38BDF8",
        GradientMid = "#0EA5E9",
        GradientEnd = "#0284C7"
    };
    
    public static ColorTheme Peaceful => new()
    {
        Name = "Peaceful",
        PrimaryColor = "#6EE7B7",      // Mint
        SecondaryColor = "#34D399",    // Green
        AccentColor = "#10B981",       // Emerald
        GradientStart = "#6EE7B7",
        GradientMid = "#34D399",
        GradientEnd = "#10B981"
    };
    
    public static ColorTheme Energetic => new()
    {
        Name = "Energetic",
        PrimaryColor = "#F87171",      // Red
        SecondaryColor = "#EF4444",    // Bright Red
        AccentColor = "#DC2626",       // Deep Red
        GradientStart = "#F87171",
        GradientMid = "#EF4444",
        GradientEnd = "#DC2626"
    };
    
    public static ColorTheme Sad => new()
    {
        Name = "Sad",
        PrimaryColor = "#94A3B8",      // Slate
        SecondaryColor = "#64748B",    // Gray Blue
        AccentColor = "#475569",       // Dark Slate
        GradientStart = "#94A3B8",
        GradientMid = "#64748B",
        GradientEnd = "#475569"
    };
    
    public static ColorTheme Fun => new()
    {
        Name = "Fun",
        PrimaryColor = "#A78BFA",      // Purple
        SecondaryColor = "#8B5CF6",    // Violet
        AccentColor = "#7C3AED",       // Deep Purple
        GradientStart = "#A78BFA",
        GradientMid = "#8B5CF6",
        GradientEnd = "#7C3AED"
    };
    
    public static ColorTheme Grateful => new()
    {
        Name = "Grateful",
        PrimaryColor = "#FDE68A",      // Light Yellow
        SecondaryColor = "#FCD34D",    // Yellow
        AccentColor = "#FBBF24",       // Gold
        GradientStart = "#FDE68A",
        GradientMid = "#FCD34D",
        GradientEnd = "#FBBF24"
    };
    
    public static ColorTheme Adventure => new()
    {
        Name = "Adventure",
        PrimaryColor = "#FB923C",      // Orange
        SecondaryColor = "#F59E0B",    // Amber
        AccentColor = "#D97706",       // Dark Amber
        GradientStart = "#FB923C",
        GradientMid = "#F59E0B",
        GradientEnd = "#D97706"
    };
    
    public static ColorTheme Chill => new()
    {
        Name = "Chill",
        PrimaryColor = "#5EEAD4",      // Teal
        SecondaryColor = "#2DD4BF",    // Cyan Teal
        AccentColor = "#14B8A6",       // Dark Teal
        GradientStart = "#5EEAD4",
        GradientMid = "#2DD4BF",
        GradientEnd = "#14B8A6"
    };
    
    public static ColorTheme Romantic => new()
    {
        Name = "Romantic",
        PrimaryColor = "#FBCFE8",      // Light Pink
        SecondaryColor = "#F9A8D4",    // Pink
        AccentColor = "#F472B6",       // Hot Pink
        GradientStart = "#FBCFE8",
        GradientMid = "#F9A8D4",
        GradientEnd = "#F472B6"
    };
    
    // Default (Cool Ocean - current theme)
    public static ColorTheme Default => new()
    {
        Name = "Default",
        PrimaryColor = "#0EA5E9",
        SecondaryColor = "#06B6D4",
        AccentColor = "#14B8A6",
        GradientStart = "#0891B2",
        GradientMid = "#06B6D4",
        GradientEnd = "#22D3EE"
    };
}
