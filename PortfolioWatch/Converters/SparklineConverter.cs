using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PortfolioWatch.Converters
{
    public class SparklineConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: History (List<double>)
            // values[1]: DayProgress (double)
            // values[2]: PreviousClose (double)
            if (values.Length >= 3 && values[0] is List<double> history && values[1] is double dayProgress && values[2] is double previousClose && history.Count > 0)
            {
                double min = Math.Min(history.Min(), previousClose);
                double max = Math.Max(history.Max(), previousClose);
                double range = max - min;
                if (range == 0) range = 1;

                double width = 60;
                double height = 20;
                
                double totalWidth = width * dayProgress;
                double step = history.Count > 1 ? totalWidth / (history.Count - 1) : 0;

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    // Force bounds to match the full width/height (0,0 to 60,20)
                    ctx.BeginFigure(new Point(0, 0), false, false);
                    ctx.BeginFigure(new Point(width, height), false, false);

                    ctx.BeginFigure(new Point(0, height - ((history[0] - min) / range * height)), false, false);
                    for (int i = 1; i < history.Count; i++)
                    {
                        double x = i * step;
                        double y = height - ((history[i] - min) / range * height);
                        ctx.LineTo(new Point(x, y), true, true);
                    }
                }
                geometry.Freeze();
                return geometry;
            }
            return Geometry.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SparklineBaselineConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: History (List<double>)
            // values[1]: PreviousClose (double)
            if (values.Length >= 2 && values[0] is List<double> history && values[1] is double previousClose && history.Count > 0)
            {
                double min = Math.Min(history.Min(), previousClose);
                double max = Math.Max(history.Max(), previousClose);
                double range = max - min;
                if (range == 0) range = 1;

                double width = 60;
                double height = 20;
                
                double baselineY = height - ((previousClose - min) / range * height);
                
                // Clamp baselineY to be within bounds (0 to height) just in case
                baselineY = Math.Max(0, Math.Min(height, baselineY));

                // Use StreamGeometry to ensure bounds match the SparklineConverter
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    // Force bounds to match the full width/height (0,0 to 60,20)
                    // This ensures alignment with the main sparkline when Stretch="Fill" is used
                    ctx.BeginFigure(new Point(0, 0), false, false);
                    ctx.BeginFigure(new Point(width, height), false, false);

                    // Draw the baseline
                    ctx.BeginFigure(new Point(0, baselineY), false, false);
                    ctx.LineTo(new Point(width, baselineY), true, true);
                }
                geometry.Freeze();
                return geometry;
            }
            return Geometry.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
