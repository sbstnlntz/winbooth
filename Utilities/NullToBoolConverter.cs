using System;
using System.Globalization;
using System.Windows.Data;

namespace winbooth.Utilities
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object param, CultureInfo culture)
            => value != null;
        public object ConvertBack(object value, Type targetType, object param, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
