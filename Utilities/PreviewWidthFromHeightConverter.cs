// Converter that derives the template button width from the preview aspect ratio.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace winbooth.Utilities
{
    public class PreviewWidthFromHeightConverter : IMultiValueConverter
    {
        /// <summary>
        /// Padding applied inside the preview border (must match XAML padding to keep spacing consistent).
        /// </summary>
        public double Padding { get; set; } = 6d;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return double.NaN;

            var height = values[0] is double h && !double.IsNaN(h) && h > 0 ? h : double.NaN;
            if (double.IsNaN(height))
                return double.NaN;

            if (values[1] is not BitmapSource bitmap || bitmap.PixelHeight <= 0)
                return double.NaN;

            var effectivePadding = Math.Max(0d, Padding);
            var innerHeight = Math.Max(0d, height - (effectivePadding * 2));
            if (innerHeight <= 0)
                return double.NaN;

            var aspectRatio = bitmap.PixelWidth / (double)bitmap.PixelHeight;
            if (aspectRatio <= 0)
                return double.NaN;

            var innerWidth = innerHeight * aspectRatio;
            var totalWidth = innerWidth + (effectivePadding * 2);

            return double.IsNaN(totalWidth) ? double.NaN : totalWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
