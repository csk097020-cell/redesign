using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>Picks a stable brand accent color for a momento's date, keyed off its id
/// so the same card always lands on the same color instead of reshuffling on refresh.</summary>
public sealed class DateAccentColorConverter : IValueConverter
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb("#D84C9A"), // Accent / pink
        Color.FromArgb("#8B63C9"), // GlowViolet
        Color.FromArgb("#49B9D3"), // GlowCyan
        Color.FromArgb("#77785C"), // Olive
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? "";
        var index = Math.Abs(key.GetHashCode()) % Palette.Length;
        return Palette[index];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
