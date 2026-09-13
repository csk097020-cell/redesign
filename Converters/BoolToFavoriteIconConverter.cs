// GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>
/// Converts a bool IsFavorite → star emoji. True = ⭐, False = ☆
/// </summary>
public sealed class BoolToFavoriteIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "⭐" : "☆";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
