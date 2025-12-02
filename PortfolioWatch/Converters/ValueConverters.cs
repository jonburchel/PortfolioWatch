using System;
using System.Globalization;
using System.Windows.Data;

namespace PortfolioWatch.Converters
{
    public class ZeroToEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d == 0)
            {
                return string.Empty;
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0.0;
                if (double.TryParse(s, out double result)) return result;
            }
            return 0.0;
        }
    }
}
