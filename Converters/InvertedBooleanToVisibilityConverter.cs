using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BingWallpaperWPF.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is true;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
