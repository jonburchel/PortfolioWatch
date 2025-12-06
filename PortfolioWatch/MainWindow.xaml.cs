using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;

namespace PortfolioWatch
{
    public partial class MainWindow : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x10000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
        }

        private DispatcherTimer _autoHideTimer;
        private PopupController _newsController = null!;
        private PopupController _earningsController = null!;
        private PopupController _optionsController = null!;
        private PopupController _insiderController = null!;
        private PopupController _rVolController = null!;

        public bool IsPinned { get; set; }
        public bool IsUserMoving { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            
            _autoHideTimer = new DispatcherTimer();
            _autoHideTimer.Interval = TimeSpan.FromMilliseconds(100);
            _autoHideTimer.Tick += (s, e) => 
            {
                if (!IsPinned)
                {
                    _autoHideTimer.Stop();
                    this.Hide();
                }
            };

            this.MouseEnter += (s, e) => _autoHideTimer.Stop();
            this.MouseLeave += (s, e) => 
            {
                if (!IsPinned && !this.IsKeyboardFocusWithin) _autoHideTimer.Start();
            };

            this.IsKeyboardFocusWithinChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    _autoHideTimer.Stop();
                }
                else
                {
                    if (!IsPinned && !this.IsMouseOver)
                    {
                        _autoHideTimer.Start();
                    }
                }
            };

            _newsController = new PopupController(NewsPopup, 500);
            _earningsController = new PopupController(EarningsPopup, 500);
            _optionsController = new PopupController(OptionsPopup, 400);
            _insiderController = new PopupController(InsiderPopup, 400);
            _rVolController = new PopupController(RVolPopup, 400);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initial positioning will be handled by App.xaml.cs
            AnimateIn();
        }

        private void AnimateIn()
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            this.BeginAnimation(OpacityProperty, anim);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Create/Find ContextMenu dynamically to avoid resource locking on the button
            var menu = (System.Windows.Controls.ContextMenu)FindResource("SharedContextMenu");
            menu.PlacementTarget = sender as UIElement;
            menu.DataContext = this.DataContext;

            // Ensure handlers are attached
            menu.Opened -= ContextMenu_Opened;
            menu.Opened += ContextMenu_Opened;
            menu.Closed -= ContextMenu_Closed;
            menu.Closed += ContextMenu_Closed;

            menu.IsOpen = true;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            _autoHideTimer.Stop();
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (!IsPinned && !this.IsMouseOver)
            {
                _autoHideTimer.Start();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                IsUserMoving = true;
                try
                {
                    this.DragMove();
                }
                finally
                {
                    IsUserMoving = false;
                }
            }
        }

        public void CancelAutoHide()
        {
            _autoHideTimer.Stop();
        }

        public void StartAutoHide()
        {
            if (!IsPinned) _autoHideTimer.Start();
        }

        public void ShowPinningTooltip()
        {
            PinningTooltip.IsOpen = true;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) =>
            {
                PinningTooltip.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

        private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
            }
        }

        private void SharesBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Explicitly update source to ensure value is committed before clearing focus
                if (sender is System.Windows.Controls.TextBox textBox)
                {
                    textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                }
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.StockSearchResult result)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.SelectSearchResultCommand.Execute(result);
                    e.Handled = true;
                }
            }
        }

        private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((System.Windows.Controls.Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }

        private void Graph_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var mousePos = e.GetPosition(element);
                UpdateGraphTooltip(element.DataContext, mousePos, element.ActualWidth, element);
            }
        }

        private void Graph_MouseLeave(object sender, MouseEventArgs e)
        {
            GraphTooltip.IsOpen = false;
        }

        private void Pie_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                PieTooltipText.Text = $"{stock.PortfolioPercentage * 100:N2}% of portfolio";
                PieTooltip.PlacementTarget = element;
                PieTooltip.IsOpen = true;
            }
        }

        private void Pie_MouseLeave(object sender, MouseEventArgs e)
        {
            PieTooltip.IsOpen = false;
        }

        private void UpdateGraphTooltip(object context, Point mousePos, double actualWidth, FrameworkElement target)
        {
            List<double>? history = null;
            List<DateTime>? timestamps = null;
            double dayProgress = 0;
            double previousClose = 0;
            string selectedRange = "1d";

            if (DataContext is ViewModels.MainViewModel mainVm)
            {
                selectedRange = mainVm.SelectedRange;
            }

            if (context is Models.Stock stock)
            {
                history = stock.History;
                timestamps = stock.Timestamps;
                dayProgress = stock.DayProgress;
                previousClose = stock.PreviousClose;
            }
            else if (context is ViewModels.MainViewModel vm)
            {
                history = vm.PortfolioHistory;
                timestamps = vm.PortfolioTimestamps;
                dayProgress = vm.PortfolioDayProgress;
                previousClose = vm.PortfolioPreviousClose;
            }

            if (history == null || history.Count == 0 || dayProgress <= 0)
            {
                GraphTooltip.IsOpen = false;
                return;
            }

            // Calculate index
            double relativeX = mousePos.X / actualWidth;
            int index = -1;

            if (selectedRange == "1d" || timestamps == null || timestamps.Count != history.Count)
            {
                // 1d Logic (Partial graph based on dayProgress)
                if (relativeX > dayProgress)
                {
                    GraphTooltip.IsOpen = false;
                    return;
                }
                index = (int)Math.Round((relativeX * (history.Count - 1)) / dayProgress);
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
                    default: startTime = timestamps.First(); break;
                }

                double totalSeconds = (endTime - startTime).TotalSeconds;
                if (totalSeconds <= 0) totalSeconds = 1;

                double hoverSeconds = relativeX * totalSeconds;
                DateTime hoverTime = startTime.AddSeconds(hoverSeconds);

                // If hovering before the first data point, show nothing
                if (hoverTime < timestamps.First())
                {
                    GraphTooltip.IsOpen = false;
                    return;
                }

                // Find closest timestamp
                int binaryIndex = timestamps.BinarySearch(hoverTime);
                if (binaryIndex >= 0)
                {
                    index = binaryIndex;
                }
                else
                {
                    // ~binaryIndex is the index of the next larger element
                    int nextIndex = ~binaryIndex;
                    int prevIndex = nextIndex - 1;

                    if (prevIndex < 0) index = nextIndex;
                    else if (nextIndex >= timestamps.Count) index = prevIndex;
                    else
                    {
                        // Check which is closer
                        double diffPrev = (hoverTime - timestamps[prevIndex]).TotalSeconds;
                        double diffNext = (timestamps[nextIndex] - hoverTime).TotalSeconds;
                        index = (diffPrev < diffNext) ? prevIndex : nextIndex;
                    }
                }
            }

            // Clamp index
            index = Math.Max(0, Math.Min(index, history.Count - 1));

            double price = history[index];
            double change = price - previousClose;
            double percent = previousClose != 0 ? (change / previousClose) : 0;

            // Update Tooltip
            TooltipPrice.Text = price.ToString("C2");
            TooltipPercent.Text = percent.ToString("+#0.00%;-#0.00%;0.00%");
            
            if (percent >= 0)
            {
                TooltipPercent.Foreground = (Brush)Application.Current.Resources["PositiveColorBrush"];
            }
            else
            {
                TooltipPercent.Foreground = (Brush)Application.Current.Resources["NegativeColorBrush"];
            }

            if (timestamps != null && index < timestamps.Count)
            {
                string format = "t"; // Default for 1d (ShortTime)
                
                if (selectedRange == "5d")
                {
                    format = "MM/dd HH:mm"; // Date + Time
                }
                else if (selectedRange != "1d")
                {
                    // 1mo, 1y, etc.
                    format = "d"; // ShortDate
                }

                TooltipTime.Text = timestamps[index].ToString(format);
                TooltipTime.Visibility = Visibility.Visible;
            }
            else
            {
                TooltipTime.Visibility = Visibility.Collapsed;
            }

            GraphTooltip.PlacementTarget = target;
            GraphTooltip.Placement = PlacementMode.Relative;
            GraphTooltip.HorizontalOffset = mousePos.X + 15;
            GraphTooltip.VerticalOffset = mousePos.Y + 15;
            GraphTooltip.IsOpen = true;
        }

        private void NewsFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
                _newsController.OnMouseEnterTarget(element, stock);
        }

        private void NewsFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _newsController.OnMouseLeaveTarget();
        }

        private void NewsFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _newsController.OnMouseLeftButtonDownTarget(element, stock);
                e.Handled = true;
            }
        }

        private void NewsPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _newsController.OnMouseEnterPopup();
        }

        private void NewsPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _newsController.OnMouseLeavePopup();
        }

        private void EarningsFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
                _earningsController.OnMouseEnterTarget(element, stock);
        }

        private void EarningsFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _earningsController.OnMouseLeaveTarget();
        }

        private void EarningsFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _earningsController.OnMouseLeftButtonDownTarget(element, stock);
                e.Handled = true;
            }
        }

        private void EarningsPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _earningsController.OnMouseEnterPopup();
        }

        private void EarningsPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _earningsController.OnMouseLeavePopup();
        }

        // Options Flag Handlers
        private void OptionsFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
                _optionsController.OnMouseEnterTarget(element, stock);
        }

        private void OptionsFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _optionsController.OnMouseLeaveTarget();
        }

        private void OptionsFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _optionsController.OnMouseLeftButtonDownTarget(element, stock);
                e.Handled = true;
            }
        }

        private void OptionsPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _optionsController.OnMouseEnterPopup();
        }

        private void OptionsPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _optionsController.OnMouseLeavePopup();
        }

        // Insider Flag Handlers
        private void InsiderFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
                _insiderController.OnMouseEnterTarget(element, stock);
        }

        private void InsiderFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _insiderController.OnMouseLeaveTarget();
        }

        private void InsiderFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _insiderController.OnMouseLeftButtonDownTarget(element, stock);
                e.Handled = true;
            }
        }

        private void InsiderPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _insiderController.OnMouseEnterPopup();
        }

        private void InsiderPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _insiderController.OnMouseLeavePopup();
        }

        // RVOL Flag Handlers
        private void RVolFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
                _rVolController.OnMouseEnterTarget(element, stock);
        }

        private void RVolFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _rVolController.OnMouseLeaveTarget();
        }

        private void RVolFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _rVolController.OnMouseLeftButtonDownTarget(element, stock);
                e.Handled = true;
            }
        }

        private void RVolPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _rVolController.OnMouseEnterPopup();
        }

        private void RVolPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _rVolController.OnMouseLeavePopup();
        }
        private class PopupController
        {
            private readonly Popup _popup;
            private readonly DispatcherTimer _openTimer;
            private readonly DispatcherTimer _closeTimer;
            private FrameworkElement? _pendingTarget;
            private Models.Stock? _pendingStock;

            public PopupController(Popup popup, int openDelayMs = 500)
            {
                _popup = popup;
                
                _openTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(openDelayMs) };
                _openTimer.Tick += OpenTimer_Tick;

                _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _closeTimer.Tick += CloseTimer_Tick;
            }

            private void OpenTimer_Tick(object? sender, EventArgs e)
            {
                _openTimer.Stop();
                if (_pendingTarget != null && _pendingStock != null)
                {
                    _popup.DataContext = _pendingStock;
                    _popup.PlacementTarget = _pendingTarget;
                    _popup.IsOpen = true;
                }
            }

            private void CloseTimer_Tick(object? sender, EventArgs e)
            {
                if (_popup.IsMouseOver) return;

                if (_popup.PlacementTarget is FrameworkElement target)
                {
                    var mousePos = Mouse.GetPosition(target);
                    if (mousePos.X >= 0 && mousePos.X <= target.ActualWidth &&
                        mousePos.Y >= 0 && mousePos.Y <= target.ActualHeight)
                    {
                        return;
                    }
                }

                _popup.IsOpen = false;
                _closeTimer.Stop();
            }

            public void OnMouseEnterTarget(FrameworkElement target, Models.Stock stock)
            {
                _closeTimer.Stop();

                if (_popup.IsOpen && _popup.DataContext == stock)
                {
                    return;
                }

                _pendingTarget = target;
                _pendingStock = stock;
                _openTimer.Start();
            }

            public void OnMouseLeaveTarget()
            {
                _openTimer.Stop();
                _closeTimer.Start();
            }

            public void OnMouseLeftButtonDownTarget(FrameworkElement target, Models.Stock stock)
            {
                _openTimer.Stop();
                _closeTimer.Stop();

                _popup.DataContext = stock;
                _popup.PlacementTarget = target;
                _popup.IsOpen = true;
            }

            public void OnMouseEnterPopup()
            {
                _closeTimer.Stop();
            }

            public void OnMouseLeavePopup()
            {
                _closeTimer.Start();
            }
        }
    }
}
