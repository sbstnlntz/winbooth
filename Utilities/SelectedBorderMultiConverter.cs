using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FotoboxApp.Models;

namespace FotoboxApp.Utilities
{
    public class SelectedBorderMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = ActiveTemplate, values[1] = SlotTemplate
            if (values[0] is TemplateItem active && values[1] is TemplateItem slot)
                return ReferenceEquals(active, slot) ? new Thickness(4) : new Thickness(0);
            return new Thickness(0);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
