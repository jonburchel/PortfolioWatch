using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PortfolioWatch.Converters
{
    public class DoubleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseDoubleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0)
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

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v && v == Visibility.Collapsed)
            {
                return true;
            }
            return false;
        }
    }

    public class InverseCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && count > 0)
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

    public class PieChartVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Expected values:
            // 0: Shares (double)
            // 1: Window ActualWidth (double)
            // 2: UIScale (double)

            if (values.Length >= 3 &&
                values[0] is double shares &&
                values[1] is double windowWidth &&
                values[2] is double uiScale)
            {
                if (shares <= 0)
                    return Visibility.Collapsed;

                // Calculate effective width
                double effectiveWidth = windowWidth / uiScale;

                // Threshold is 500 as per requirements
                if (effectiveWidth > 500)
                    return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TaxPieVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Expected values:
            // 0: IsEditing (bool)
            // 1: IsPortfolioMode (bool)
            // 2: TotalValue (decimal or double)

            if (values.Length >= 3 &&
                values[0] is bool isEditing &&
                values[1] is bool isPortfolioMode)
            {
                double totalValue = 0;
                if (values[2] is decimal d) totalValue = (double)d;
                else if (values[2] is double db) totalValue = db;
                else if (values[2] is float f) totalValue = (double)f;
                else if (values[2] is int i) totalValue = (double)i;

                if (isEditing) return Visibility.Collapsed;
                if (!isPortfolioMode) return Visibility.Collapsed;
                if (totalValue <= 0) return Visibility.Collapsed;

                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
