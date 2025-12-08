using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PortfolioWatch.Converters
{
    public abstract class SparklineBaseConverter : IMultiValueConverter
    {
        protected List<Point> GetPoints(object[] values, out double baselineY)
        {
            baselineY = 0;
            // values[0]: History (List<double>)
            // values[1]: DayProgress (double)
            // values[2]: PreviousClose (double)
            // values[3]: Timestamps (List<DateTime>) - Optional
            // values[4]: SelectedRange (string) - Optional

            if (values.Length >= 3 && values[0] is List<double> history && values[1] is double dayProgress && values[2] is double previousClose && history.Count > 0)
            {
                List<DateTime>? timestamps = (values.Length > 3) ? values[3] as List<DateTime> : null;
                string selectedRange = (values.Length > 4) ? values[4] as string ?? "1d" : "1d";

                double min = Math.Min(history.Min(), previousClose);
                double max = Math.Max(history.Max(), previousClose);
                double range = max - min;
                if (range == 0) range = 1;

                double width = 60;
                double height = 20;

                baselineY = height - ((previousClose - min) / range * height);
                // Clamp baselineY
                baselineY = Math.Max(0, Math.Min(height, baselineY));

                var points = new List<Point>();

                if (selectedRange == "1d" || timestamps == null || timestamps.Count != history.Count || timestamps.Count == 0)
                {
                    // Original logic for 1d or missing timestamps
                    double totalWidth = width * dayProgress;
                    double step = history.Count > 1 ? totalWidth / (history.Count - 1) : 0;

                    for (int i = 0; i < history.Count; i++)
                    {
                        double x = i * step;
                        double y = height - ((history[i] - min) / range * height);

                        if (!double.IsNaN(x) && !double.IsInfinity(x) && !double.IsNaN(y) && !double.IsInfinity(y))
                        {
                            points.Add(new Point(x, y));
                        }
                    }
                }
                else
                {
                    // Time-based logic for historical ranges
                    DateTime endTime = timestamps.Last();
                    DateTime startTime;

                    switch (selectedRange)
                    {
                        case "5d": startTime = endTime.AddDays(-5); break;
                        case "1mo": startTime = endTime.AddMonths(-1); break;
                        case "1y": startTime = endTime.AddYears(-1); break;
                        case "5y": startTime = endTime.AddYears(-5); break;
                        case "10y": startTime = endTime.AddYears(-10); break;
                        default: startTime = timestamps.First(); break; // Fallback
                    }

                    double totalSeconds = (endTime - startTime).TotalSeconds;
                    if (totalSeconds <= 0) totalSeconds = 1;

                    for (int i = 0; i < history.Count; i++)
                    {
                        double timeOffset = (timestamps[i] - startTime).TotalSeconds;
                        double x = (timeOffset / totalSeconds) * width;
                        double y = height - ((history[i] - min) / range * height);

                        if (!double.IsNaN(x) && !double.IsInfinity(x) && !double.IsNaN(y) && !double.IsInfinity(y))
                        {
                            points.Add(new Point(x, y));
                        }
                    }
                }
                return points;
            }
            return new List<Point>();
        }

        public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SparklinePositiveConverter : SparklineBaseConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var points = GetPoints(values, out double baselineY);
            if (points.Count < 2) return Geometry.Empty;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                // Force bounds
                ctx.BeginFigure(new Point(0, 0), false, false);
                ctx.BeginFigure(new Point(60, 20), false, false);

                bool isFigureStarted = false;

                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point p1 = points[i];
                    Point p2 = points[i + 1];

                    // In screen coords, "Above" baseline means Y < baselineY
                    // "Positive" means Value > PreviousClose, so Y < baselineY
                    bool isP1Pos = p1.Y <= baselineY + 0.0001; // Epsilon for float comparison
                    bool isP2Pos = p2.Y <= baselineY + 0.0001;

                    if (isP1Pos)
                    {
                        if (!isFigureStarted)
                        {
                            ctx.BeginFigure(p1, false, false);
                            isFigureStarted = true;
                        }

                        if (isP2Pos)
                        {
                            ctx.LineTo(p2, true, true);
                        }
                        else
                        {
                            // Crossing down
                            Point intersection = GetIntersection(p1, p2, baselineY);
                            ctx.LineTo(intersection, true, true);
                            isFigureStarted = false;
                        }
                    }
                    else
                    {
                        if (isP2Pos)
                        {
                            // Crossing up
                            Point intersection = GetIntersection(p1, p2, baselineY);
                            ctx.BeginFigure(intersection, false, false);
                            ctx.LineTo(p2, true, true);
                            isFigureStarted = true;
                        }
                        else
                        {
                            isFigureStarted = false;
                        }
                    }
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private Point GetIntersection(Point p1, Point p2, double baselineY)
        {
            if (Math.Abs(p2.Y - p1.Y) < 0.0001) return new Point(p1.X, baselineY);
            double t = (baselineY - p1.Y) / (p2.Y - p1.Y);
            double x = p1.X + t * (p2.X - p1.X);
            return new Point(x, baselineY);
        }
    }

    public class SparklineNegativeConverter : SparklineBaseConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var points = GetPoints(values, out double baselineY);
            if (points.Count < 2) return Geometry.Empty;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                // Force bounds
                ctx.BeginFigure(new Point(0, 0), false, false);
                ctx.BeginFigure(new Point(60, 20), false, false);

                bool isFigureStarted = false;

                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point p1 = points[i];
                    Point p2 = points[i + 1];

                    // "Negative" means Value < PreviousClose, so Y > baselineY
                    bool isP1Neg = p1.Y >= baselineY - 0.0001;
                    bool isP2Neg = p2.Y >= baselineY - 0.0001;

                    if (isP1Neg)
                    {
                        if (!isFigureStarted)
                        {
                            ctx.BeginFigure(p1, false, false);
                            isFigureStarted = true;
                        }

                        if (isP2Neg)
                        {
                            ctx.LineTo(p2, true, true);
                        }
                        else
                        {
                            // Crossing up (into positive/above baseline)
                            Point intersection = GetIntersection(p1, p2, baselineY);
                            ctx.LineTo(intersection, true, true);
                            isFigureStarted = false;
                        }
                    }
                    else
                    {
                        if (isP2Neg)
                        {
                            // Crossing down (into negative/below baseline)
                            Point intersection = GetIntersection(p1, p2, baselineY);
                            ctx.BeginFigure(intersection, false, false);
                            ctx.LineTo(p2, true, true);
                            isFigureStarted = true;
                        }
                        else
                        {
                            isFigureStarted = false;
                        }
                    }
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private Point GetIntersection(Point p1, Point p2, double baselineY)
        {
            if (Math.Abs(p2.Y - p1.Y) < 0.0001) return new Point(p1.X, baselineY);
            double t = (baselineY - p1.Y) / (p2.Y - p1.Y);
            double x = p1.X + t * (p2.X - p1.X);
            return new Point(x, baselineY);
        }
    }

    public class SparklineBaselineConverter : SparklineBaseConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: History (List<double>)
            // values[1]: PreviousClose (double)
            // Note: Base GetPoints expects values[1] to be DayProgress, values[2] to be PreviousClose
            // We need to adapt or just use the logic directly since this converter signature is different in XAML usage?
            // Actually, let's check how it's used.
            // In MainWindow.xaml (assumed):
            // <MultiBinding Converter="{StaticResource SparklineBaselineConverter}">
            //    <Binding Path="History" />
            //    <Binding Path="PreviousClose" />
            // </MultiBinding>
            
            // The base GetPoints expects a different signature. Let's just reimplement simple logic here.
            
            if (values.Length >= 2 && values[0] is List<double> history && values[1] is double previousClose && history.Count > 0)
            {
                double min = Math.Min(history.Min(), previousClose);
                double max = Math.Max(history.Max(), previousClose);
                double range = max - min;
                if (range == 0) range = 1;

                double width = 60;
                double height = 20;
                
                double baselineY = height - ((previousClose - min) / range * height);
                baselineY = Math.Max(0, Math.Min(height, baselineY));

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(0, 0), false, false);
                    ctx.BeginFigure(new Point(width, height), false, false);
                    ctx.BeginFigure(new Point(0, baselineY), false, false);
                    ctx.LineTo(new Point(width, baselineY), true, true);
                }
                geometry.Freeze();
                return geometry;
            }
            return Geometry.Empty;
        }
    }
}
