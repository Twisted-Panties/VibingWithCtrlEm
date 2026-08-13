using System.Globalization;
using System.Windows.Data;

namespace VibingWithCtrlEm.Converters;

/// <summary>
/// Converts an integer millisecond value to a human-readable seconds string.
/// e.g. 200 → "0.2 s",  1000 → "1 s",  2000 → "2 s"
/// Uses up to three significant decimal places, dropping trailing zeros.
/// </summary>
[ValueConversion(typeof(int), typeof(string))]
public sealed class MsToSecondsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int ms)
            return $"{ms / 1000.0:0.###} s";

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("MsToSecondsConverter is one-way only.");
}
