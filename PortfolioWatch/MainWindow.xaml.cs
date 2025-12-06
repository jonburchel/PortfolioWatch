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
        private DispatcherTimer _autoHideTimer;
        private DispatcherTimer _newsPopupTimer;
        private DispatcherTimer _newsOpenTimer;
        private FrameworkElement? _pendingNewsTarget;
        private Models.Stock? _pendingNewsStock;

        private DispatcherTimer _earningsPopupTimer;
        private DispatcherTimer _earningsOpenTimer;
        private FrameworkElement? _pendingEarningsTarget;
        private Models.Stock? _pendingEarningsStock;

        private DispatcherTimer _optionsPopupTimer;
        private DispatcherTimer _optionsOpenTimer;
        private FrameworkElement? _pendingOptionsTarget;
        private Models.Stock? _pendingOptionsStock;

        private DispatcherTimer _insiderPopupTimer;
        private DispatcherTimer _insiderOpenTimer;
        private FrameworkElement? _pendingInsiderTarget;
        private Models.Stock? _pendingInsiderStock;

        private DispatcherTimer _rVolPopupTimer;
        private DispatcherTimer _rVolOpenTimer;
        private FrameworkElement? _pendingRVolTarget;
        private Models.Stock? _pendingRVolStock;

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

            _newsPopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _newsPopupTimer.Tick += (s, e) =>
            {
                // Check if mouse is over Popup
                if (NewsPopup.IsMouseOver) return;

                // Check if mouse is over Target (Flag)
                // We use bounds check instead of IsMouseOver because IsMouseOver can be false 
                // when the Popup window is active/focused, causing flickering.
                if (NewsPopup.PlacementTarget is FrameworkElement target)
                {
                    var mousePos = Mouse.GetPosition(target);
                    if (mousePos.X >= 0 && mousePos.X <= target.ActualWidth &&
                        mousePos.Y >= 0 && mousePos.Y <= target.ActualHeight)
                    {
                        return;
                    }
                }

                NewsPopup.IsOpen = false;
                _newsPopupTimer.Stop();
            };

            _newsOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _newsOpenTimer.Tick += (s, e) =>
            {
                _newsOpenTimer.Stop();
                if (_pendingNewsTarget != null && _pendingNewsStock != null)
                {
                    NewsPopup.DataContext = _pendingNewsStock;
                    NewsPopup.PlacementTarget = _pendingNewsTarget;
                    NewsPopup.IsOpen = true;
                }
            };

            _earningsPopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _earningsPopupTimer.Tick += (s, e) =>
            {
                // Check if mouse is over Popup
                if (EarningsPopup.IsMouseOver) return;

                // Check if mouse is over Target (Flag)
                if (EarningsPopup.PlacementTarget is FrameworkElement target)
                {
                    var mousePos = Mouse.GetPosition(target);
                    if (mousePos.X >= 0 && mousePos.X <= target.ActualWidth &&
                        mousePos.Y >= 0 && mousePos.Y <= target.ActualHeight)
                    {
                        return;
                    }
                }

                EarningsPopup.IsOpen = false;
                _earningsPopupTimer.Stop();
            };

            _earningsOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _earningsOpenTimer.Tick += (s, e) =>
            {
                _earningsOpenTimer.Stop();
                if (_pendingEarningsTarget != null && _pendingEarningsStock != null)
                {
                    EarningsPopup.DataContext = _pendingEarningsStock;
                    EarningsPopup.PlacementTarget = _pendingEarningsTarget;
                    EarningsPopup.IsOpen = true;
                }
            };

            // Options Popup Timers
            _optionsPopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _optionsPopupTimer.Tick += (s, e) =>
            {
                if (OptionsPopup.IsMouseOver) return;
                if (OptionsPopup.PlacementTarget is FrameworkElement target)
                {
                    var mousePos = Mouse.GetPosition(target);
                    if (mousePos.X >= 0 && mousePos.X <= target.ActualWidth &&
                        mousePos.Y >= 0 && mousePos.Y <= target.ActualHeight)
                    {
                        return;
                    }
                }
                OptionsPopup.IsOpen = false;
                _optionsPopupTimer.Stop();
            };

            _optionsOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _optionsOpenTimer.Tick += (s, e) =>
            {
                _optionsOpenTimer.Stop();
                if (_pendingOptionsTarget != null && _pendingOptionsStock != null)
                {
                    OptionsPopup.DataContext = _pendingOptionsStock;
                    OptionsPopup.PlacementTarget = _pendingOptionsTarget;
                    OptionsPopup.IsOpen = true;
                }
            };

            // Insider Popup Timers
            _insiderPopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _insiderPopupTimer.Tick += (s, e) =>
            {
                if (InsiderPopup.IsMouseOver) return;
                if (InsiderPopup.PlacementTarget is FrameworkElement target)
                {
                    var mousePos = Mouse.GetPosition(target);
                    if (mousePos.X >= 0 && mousePos.X <= target.ActualWidth &&
                        mousePos.Y >= 0 && mousePos.Y <= target.ActualHeight)
                    {
                        return;
                    }
                }
                InsiderPopup.IsOpen = false;
                _insiderPopupTimer.Stop();
            };

            _insiderOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _insiderOpenTimer.Tick += (s, e) =>
            {
                _insiderOpenTimer.Stop();
                if (_pendingInsiderTarget != null && _pendingInsiderStock != null)
                {
                    InsiderPopup.DataContext = _pendingInsiderStock;
                    InsiderPopup.PlacementTarget = _pendingInsiderTarget;
                    InsiderPopup.IsOpen = true;
                }
            };

            // RVOL Popup Timers
            _rVolPopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _rVolPopupTimer.Tick += (s, e) =>
            {
                if (RVolPopup.IsMouseOver) return;
                if (RVolPopup.PlacementTarget is FrameworkElement target)
                {
                    var mousePos = Mouse.GetPosition(target);
                    if (mousePos.X >= 0 && mousePos.X <= target.ActualWidth &&
                        mousePos.Y >= 0 && mousePos.Y <= target.ActualHeight)
                    {
                        return;
                    }
                }
                RVolPopup.IsOpen = false;
                _rVolPopupTimer.Stop();
            };

            _rVolOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _rVolOpenTimer.Tick += (s, e) =>
            {
                _rVolOpenTimer.Stop();
                if (_pendingRVolTarget != null && _pendingRVolStock != null)
                {
                    RVolPopup.DataContext = _pendingRVolStock;
                    RVolPopup.PlacementTarget = _pendingRVolTarget;
                    RVolPopup.IsOpen = true;
                }
            };
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
            // The graph draws from 0 to (60 * dayProgress) in a 60-unit wide coordinate space.
            // This 60-unit space is stretched to actualWidth.
            // So the "drawn" width in pixels is actualWidth * dayProgress.
            // If mouse is beyond this, we clamp to the last point.

            // Map mouseX to logical X (0..60)
            // double logicalX = (mousePos.X / actualWidth) * 60;
            
            // Map logical X to index
            // index = logicalX * (count - 1) / (60 * dayProgress)
            // index = (mousePos.X / actualWidth) * (count - 1) / dayProgress

            double relativeX = mousePos.X / actualWidth;
            int index = (int)Math.Round((relativeX * (history.Count - 1)) / dayProgress);

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
            {
                _newsPopupTimer.Stop();

                // Prevent flickering by checking if we're already showing this stock's news
                if (NewsPopup.IsOpen && NewsPopup.DataContext == stock)
                {
                    return;
                }

                _pendingNewsTarget = element;
                _pendingNewsStock = stock;
                _newsOpenTimer.Start();
            }
        }

        private void NewsFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _newsOpenTimer.Stop();
            _newsPopupTimer.Start();
        }

        private void NewsFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _newsOpenTimer.Stop();
                _newsPopupTimer.Stop();

                NewsPopup.DataContext = stock;
                NewsPopup.PlacementTarget = element;
                NewsPopup.IsOpen = true;
                
                e.Handled = true;
            }
        }

        private void NewsPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _newsPopupTimer.Stop();
        }

        private void NewsPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _newsPopupTimer.Start();
        }

        private void EarningsFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _earningsPopupTimer.Stop();

                // Prevent flickering by checking if we're already showing this stock's earnings
                if (EarningsPopup.IsOpen && EarningsPopup.DataContext == stock)
                {
                    return;
                }

                _pendingEarningsTarget = element;
                _pendingEarningsStock = stock;
                _earningsOpenTimer.Start();
            }
        }

        private void EarningsFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _earningsOpenTimer.Stop();
            _earningsPopupTimer.Start();
        }

        private void EarningsFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _earningsOpenTimer.Stop();
                _earningsPopupTimer.Stop();

                EarningsPopup.DataContext = stock;
                EarningsPopup.PlacementTarget = element;
                EarningsPopup.IsOpen = true;
                
                e.Handled = true;
            }
        }

        private void EarningsPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _earningsPopupTimer.Stop();
        }

        private void EarningsPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _earningsPopupTimer.Start();
        }

        // Options Flag Handlers
        private void OptionsFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _optionsPopupTimer.Stop();
                if (OptionsPopup.IsOpen && OptionsPopup.DataContext == stock) return;
                _pendingOptionsTarget = element;
                _pendingOptionsStock = stock;
                _optionsOpenTimer.Start();
            }
        }

        private void OptionsFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _optionsOpenTimer.Stop();
            _optionsPopupTimer.Start();
        }

        private void OptionsFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _optionsOpenTimer.Stop();
                _optionsPopupTimer.Stop();
                OptionsPopup.DataContext = stock;
                OptionsPopup.PlacementTarget = element;
                OptionsPopup.IsOpen = true;
                e.Handled = true;
            }
        }

        private void OptionsPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _optionsPopupTimer.Stop();
        }

        private void OptionsPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _optionsPopupTimer.Start();
        }

        // Insider Flag Handlers
        private void InsiderFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _insiderPopupTimer.Stop();
                if (InsiderPopup.IsOpen && InsiderPopup.DataContext == stock) return;
                _pendingInsiderTarget = element;
                _pendingInsiderStock = stock;
                _insiderOpenTimer.Start();
            }
        }

        private void InsiderFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _insiderOpenTimer.Stop();
            _insiderPopupTimer.Start();
        }

        private void InsiderFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _insiderOpenTimer.Stop();
                _insiderPopupTimer.Stop();
                InsiderPopup.DataContext = stock;
                InsiderPopup.PlacementTarget = element;
                InsiderPopup.IsOpen = true;
                e.Handled = true;
            }
        }

        private void InsiderPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _insiderPopupTimer.Stop();
        }

        private void InsiderPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _insiderPopupTimer.Start();
        }

        // RVOL Flag Handlers
        private void RVolFlag_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _rVolPopupTimer.Stop();
                if (RVolPopup.IsOpen && RVolPopup.DataContext == stock) return;
                _pendingRVolTarget = element;
                _pendingRVolStock = stock;
                _rVolOpenTimer.Start();
            }
        }

        private void RVolFlag_MouseLeave(object sender, MouseEventArgs e)
        {
            _rVolOpenTimer.Stop();
            _rVolPopupTimer.Start();
        }

        private void RVolFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.Stock stock)
            {
                _rVolOpenTimer.Stop();
                _rVolPopupTimer.Stop();
                RVolPopup.DataContext = stock;
                RVolPopup.PlacementTarget = element;
                RVolPopup.IsOpen = true;
                e.Handled = true;
            }
        }

        private void RVolPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _rVolPopupTimer.Stop();
        }

        private void RVolPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _rVolPopupTimer.Start();
        }
    }
}
