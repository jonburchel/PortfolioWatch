using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PortfolioWatch.Models;

namespace PortfolioWatch.Converters
{
    public class MultiIndexGraphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ObservableCollection<Stock> indexes && indexes.Count > 0 && parameter is string symbol)
            {
                var targetIndex = indexes.FirstOrDefault(s => s.Symbol == symbol);
                if (targetIndex == null || targetIndex.History == null || targetIndex.History.Count == 0)
                    return Geometry.Empty;

                // 1. Calculate % Change History for ALL indexes to determine global scale
                double globalMin = 0;
                double globalMax = 0;
                bool first = true;

                foreach (var index in indexes)
                {
                    if (index.History == null || index.History.Count == 0 || index.PreviousClose == 0) continue;

                    foreach (var price in index.History)
                    {
                        double pctChange = (price - index.PreviousClose) / index.PreviousClose;
                        if (first)
                        {
                            globalMin = pctChange;
                            globalMax = pctChange;
                            first = false;
                        }
                        else
                        {
                            if (pctChange < globalMin) globalMin = pctChange;
                            if (pctChange > globalMax) globalMax = pctChange;
                        }
                    }
                }

                // Add some padding to the range
                double range = globalMax - globalMin;
                if (range == 0) range = 0.01; // Avoid divide by zero
                
                // 2. Generate Geometry for the target index
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    double width = 100.0; // Normalized width
                    double height = 50.0; // Normalized height

                    var history = targetIndex.History;
                    double prevClose = targetIndex.PreviousClose;
                    
                    if (prevClose == 0) return Geometry.Empty;

                    // Calculate points
                    // X: 0 to Width
                    // Y: Scaled from GlobalMin to GlobalMax (inverted because Y grows down)
                    
                    // Y = Height - ((PctChange - GlobalMin) / Range) * Height
                    
                    for (int i = 0; i < history.Count; i++)
                    {
                        double pctChange = (history[i] - prevClose) / prevClose;
                        
                        double x = (double)i / (history.Count - 1) * width;
                        double normalizedY = (pctChange - globalMin) / range;
                        double y = height - (normalizedY * height);

                        if (i == 0)
                        {
                            ctx.BeginFigure(new Point(x, y), false, false);
                        }
                        else
                        {
                            ctx.LineTo(new Point(x, y), true, true);
                        }
                    }
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
