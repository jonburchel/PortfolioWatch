using System;
using System.Globalization;
using System.Windows;
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
            
            if (parameter is string format && value is IFormattable formattable)
            {
                return formattable.ToString(format, culture);
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

    public class StringEqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? parameter : Binding.DoNothing;
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? s = value as string;
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NewsItemBorderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] != null && values[1] is System.Collections.IList list)
            {
                var item = values[0];
                if (list.Count > 0 && list[list.Count - 1] == item)
                {
                    return new Thickness(0);
                }
            }
            return new Thickness(0, 0, 0, 1);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NewsItemSpacingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] != null && values[1] is System.Collections.IList list)
            {
                var item = values[0];
                if (list.Count > 0 && list[list.Count - 1] == item)
                {
                    return new Thickness(0);
                }
            }
            return new Thickness(0, 0, 0, 4);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FloatingWindowOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double opacity)
            {
                if (opacity >= 1.0)
                {
                    return 0.95;
                }
                else
                {
                    // Map 0.0 -> 0.99 to 0.10 -> 0.85
                    // Formula: 0.10 + (opacity / 0.99) * (0.85 - 0.10)
                    return 0.10 + (opacity / 0.99) * 0.75;
                }
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SignalStrengthToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double strength && parameter is string paramStr && int.TryParse(paramStr, out int barIndex))
            {
                // strength is 0-5. barIndex is 1-5.
                // If strength >= barIndex, full opacity.
                // If strength is between barIndex-1 and barIndex (e.g. 3.5 for bar 4), maybe partial?
                // User said "middle 'faded' state".
                // Let's keep it simple: if strength >= barIndex, 1.0. Else 0.2.
                // Or maybe support partial bars?
                // "5 bars that can each have a middle 'faded' state"
                // Maybe:
                // If strength >= barIndex: 1.0
                // If strength > barIndex - 1: 0.5 (half bar?)
                // Else: 0.2 (empty bar)
                
                if (strength >= barIndex) return 0.85;
                if (strength > barIndex - 1) return 0.5; // Partial/Faded
                return 0.15; // Empty/Background
            }
            return 0.15;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EarningsStatusVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If status is "Upcoming", hide the signal bars
            if (value is string status && status == "Upcoming")
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
