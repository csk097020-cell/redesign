using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>Converts IsAdmin bool to "Remove Admin" / "Make Admin" button label.</summary>
public class AdminLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Remove Admin" : "Make Admin";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
