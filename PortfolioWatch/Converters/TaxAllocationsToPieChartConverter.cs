using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PortfolioWatch.Models;

namespace PortfolioWatch.Converters
{
    public class TaxAllocationsToPieChartConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<TaxAllocation> allocations)
            {
                var drawingGroup = new DrawingGroup();
                double currentAngle = -90; // Start at 12 o'clock
                Point center = new Point(50, 50);
                double radius = 50;

                foreach (var allocation in allocations)
                {
                    if (allocation.Percentage <= 0) continue;

                    double sweepAngle = allocation.Percentage / 100.0 * 360;
                    if (sweepAngle > 360) sweepAngle = 360;

                    // Create geometry for the slice
                    Geometry sliceGeometry;
                    if (sweepAngle >= 360)
                    {
                        sliceGeometry = new EllipseGeometry(center, radius, radius);
                    }
                    else
                    {
                        double startRad = currentAngle * Math.PI / 180;
                        double endRad = (currentAngle + sweepAngle) * Math.PI / 180;

                        Point startPoint = new Point(
                            center.X + radius * Math.Cos(startRad),
                            center.Y + radius * Math.Sin(startRad));

                        Point endPoint = new Point(
                            center.X + radius * Math.Cos(endRad),
                            center.Y + radius * Math.Sin(endRad));

                        StreamGeometry geom = new StreamGeometry();
                        using (StreamGeometryContext ctx = geom.Open())
                        {
                            ctx.BeginFigure(center, true, true);
                            ctx.LineTo(startPoint, true, false);
                            ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true, false);
                        }
                        sliceGeometry = geom;
                    }

                    // Convert color string to Brush
                    Brush brush;
                    try
                    {
                        brush = new SolidColorBrush(allocation.Color);
                    }
                    catch
                    {
                        brush = Brushes.Gray;
                    }

                    drawingGroup.Children.Add(new GeometryDrawing(brush, null, sliceGeometry));

                    currentAngle += sweepAngle;
                }

                var image = new DrawingImage(drawingGroup);
                image.Freeze();
                return image;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
