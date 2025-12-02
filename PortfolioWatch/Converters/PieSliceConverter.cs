using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PortfolioWatch.Converters
{
    public class PieSliceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                if (percentage <= 0.001) // Effectively zero
                    return Geometry.Empty;

                if (percentage >= 0.999) // Full circle
                {
                    return new EllipseGeometry(new Point(11, 11), 10, 10);
                }

                // Calculate end angle
                // Start is -90 degrees (12 o'clock)
                double startAngle = -90;
                double sweepAngle = percentage * 360;
                double endAngle = startAngle + sweepAngle;

                // Convert to radians
                double startRad = startAngle * Math.PI / 180;
                double endRad = endAngle * Math.PI / 180;

                // Center and radius
                Point center = new Point(11, 11);
                double radius = 10;

                // Calculate start and end points
                Point startPoint = new Point(
                    center.X + radius * Math.Cos(startRad),
                    center.Y + radius * Math.Sin(startRad));

                Point endPoint = new Point(
                    center.X + radius * Math.Cos(endRad),
                    center.Y + radius * Math.Sin(endRad));

                // Create geometry
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(center, true, true);
                    ctx.LineTo(startPoint, true, false);
                    ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true, false);
                }

                return geometry;
            }

            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
