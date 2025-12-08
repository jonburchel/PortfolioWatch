using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PortfolioWatch.Helpers
{
    public class DragAdorner : Adorner
    {
        private readonly Rectangle _child;
        private double _offsetLeft;
        private double _offsetTop;

        public DragAdorner(UIElement adornedElement, Visual visualBrush, double opacity)
            : base(adornedElement)
        {
            _child = new Rectangle
            {
                Width = adornedElement.RenderSize.Width,
                Height = adornedElement.RenderSize.Height,
                Fill = new VisualBrush(visualBrush) { Stretch = Stretch.None },
                Opacity = opacity,
                IsHitTestVisible = false
            };
        }

        public void UpdatePosition(double left, double top)
        {
            _offsetLeft = left;
            _offsetTop = top;
            if (Parent is AdornerLayer layer)
            {
                layer.Update(AdornedElement);
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _child.Measure(constraint);
            return _child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _child.Arrange(new Rect(_child.DesiredSize));
            return finalSize;
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var result = new GeneralTransformGroup();
            result.Children.Add(base.GetDesiredTransform(transform));
            result.Children.Add(new TranslateTransform(_offsetLeft, _offsetTop));
            return result;
        }

        protected override Visual GetVisualChild(int index) => _child;
        protected override int VisualChildrenCount => 1;
    }
}
