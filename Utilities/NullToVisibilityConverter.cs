// Value converter turning null references into collapsed visibility.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace winbooth.Utilities
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
