using System.Globalization;

namespace MomentaryMomentos.Converters;

/// <summary>
/// Converter that checks if an item exists in a collection
/// </summary>
public class ContainsItemConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var collection = value as System.Collections.IEnumerable;
        if (collection == null)
            return false;

        foreach (var item in collection)
        {
            if (item == parameter)
                return true;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way converter — converting back from bool to collection is not supported.
        return Binding.DoNothing;
    }
}
