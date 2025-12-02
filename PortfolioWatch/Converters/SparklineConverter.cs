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
            if (values.Length >= 2 && values[0] is List<double> history && values[1] is double dayProgress && history.Count > 1)
            {
                double min = history.Min();
                double max = history.Max();
                double range = max - min;
                if (range == 0) range = 1;

                double width = 60;
                double height = 20;
                
                // Calculate step based on full day width
                // If dayProgress is 1.0, we use full width.
                // If dayProgress is 0.5, the current history should span 50% of width.
                // So the step size should be: width * dayProgress / (history.Count - 1)
                // Wait, if history.Count represents "points so far", then:
                // X_last = width * dayProgress.
                // X_i = i * (width * dayProgress) / (history.Count - 1).
                
                // However, if history.Count is small (e.g. 2 points at start of day), step is large?
                // Yes, but the total width covered is small.
                // Example: 2 points, progress 0.01. Width covered = 0.6px. Step = 0.6px.
                // Example: 390 points, progress 1.0. Width covered = 60px. Step = 60/389 = 0.15px.
                
                // Edge case: history.Count = 1. Handled by check > 1.
                
                double totalWidth = width * dayProgress;
                double step = totalWidth / (history.Count - 1);

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    // Force bounds to match the full width/height (0,0 to 60,20)
                    // This ensures Stretch="Fill" works correctly even for partial days
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

    public class SparklineBaselineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<double> history && history.Count > 0)
            {
                double min = history.Min();
                double max = history.Max();
                double range = max - min;
                if (range == 0) range = 1;

                double width = 60;
                double height = 20;
                
                double baselineY = height - ((history[0] - min) / range * height);
                LineGeometry baseline = new LineGeometry(new Point(0, baselineY), new Point(width, baselineY));
                baseline.Freeze();
                return baseline;
            }
            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
