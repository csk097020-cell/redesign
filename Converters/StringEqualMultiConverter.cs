using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>IMultiValueConverter that returns true when two bound string values are equal.</summary>
public class StringEqualMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2) return false;
        return values[0]?.ToString() == values[1]?.ToString();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
