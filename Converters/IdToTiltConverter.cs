using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>Gives each momento's polaroid a small, stable scatter tilt keyed off its id,
/// so the Recent Momentos row reads like a loose stack of photos instead of a grid.</summary>
public sealed class IdToTiltConverter : IValueConverter
{
    private static readonly double[] Angles = { -3, 2, -1.5, 3, -2 };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? "";
        var index = Math.Abs(key.GetHashCode()) % Angles.Length;
        return Angles[index];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
