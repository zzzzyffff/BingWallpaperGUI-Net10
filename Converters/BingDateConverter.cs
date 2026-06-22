using System;
using System.Globalization;
using System.Windows.Data;

namespace BingWallpaperWPF.Converters;

[ValueConversion(typeof(string), typeof(string))]
public class BingDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? dateStr = value?.ToString();
        if (dateStr?.Length == 8)
        {
            return $"{dateStr[..4]}-{dateStr[4..6]}-{dateStr[6..]}";
        }
        return dateStr ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
