using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>Turns the 1–4 guided Relive step into a 0.25–1.0 progress value.</summary>
public sealed class ReliveStepProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var step = value is int i ? i : 1;
        return Math.Clamp(step, 1, 4) / 4.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
