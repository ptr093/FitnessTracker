using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace FitnessTracker.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        // Kolory, które chcemy uzyskać. Można tu wpisać dowolne.
        public Color TrueColor { get; set; } = Colors.LightGray; // Aktywny przycisk
        public Color FalseColor { get; set; } = Colors.Transparent; // Nieaktywny przycisk

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }
            return FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}