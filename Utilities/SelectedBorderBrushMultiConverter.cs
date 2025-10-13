using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FotoboxApp.Models;

namespace FotoboxApp.Utilities
{
    public class SelectedBorderBrushMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]=ActiveTemplate, values[1]=SlotTemplate
            if (values[0] is TemplateItem active && values[1] is TemplateItem slot)
                return ReferenceEquals(active, slot)
                    ? new SolidColorBrush(Color.FromRgb(200, 16, 46)) // Rot wie Banner
                    : new SolidColorBrush(Color.FromRgb(160, 160, 160)); // Hellgrau
            return new SolidColorBrush(Color.FromRgb(160, 160, 160));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
