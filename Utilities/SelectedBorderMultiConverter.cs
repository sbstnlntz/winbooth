// Multi value converter that returns highlight colors and borders for selected template slots.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using winbooth.Models;

namespace winbooth.Utilities
{
    public class SelectedBorderMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = ActiveTemplate, values[1] = SlotTemplate
            var treatNullAsSelected = parameter is string parameterString
                                      && string.Equals(parameterString, "TreatNullAsSelected", StringComparison.OrdinalIgnoreCase);

            var activeUnset = values.Length == 0 || ReferenceEquals(values[0], DependencyProperty.UnsetValue);
            var slotUnset = values.Length < 2 || ReferenceEquals(values[1], DependencyProperty.UnsetValue);

            var active = activeUnset ? null : values[0] as TemplateItem;
            var slot = slotUnset ? null : values[1] as TemplateItem;

            if (active != null && slot != null)
                return ReferenceEquals(active, slot) ? new Thickness(4) : new Thickness(0);

            if (treatNullAsSelected)
            {
                var activeIsNull = activeUnset || values[0] == null;
                var slotIsNull = slotUnset || values[1] == null;
                if (activeIsNull && slotIsNull)
                    return new Thickness(4);
            }

            return new Thickness(0);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
