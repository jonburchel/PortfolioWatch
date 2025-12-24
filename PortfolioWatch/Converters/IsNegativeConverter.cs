using System;
using System.Globalization;
using System.Windows.Data;

namespace PortfolioWatch.Converters
{
    public class IsNegativeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return d < 0;
            }
            if (value is decimal dec)
            {
                return dec < 0;
            }
            if (value is float f)
            {
                return f < 0;
            }
            if (value is int i)
            {
                return i < 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
