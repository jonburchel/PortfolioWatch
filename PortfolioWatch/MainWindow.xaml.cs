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
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                // Ensure DataContext is set
                btn.ContextMenu.DataContext = this.DataContext;

                // Ensure handlers are attached
                btn.ContextMenu.Opened -= ContextMenu_Opened;
                btn.ContextMenu.Opened += ContextMenu_Opened;
                btn.ContextMenu.Closed -= ContextMenu_Closed;
                btn.ContextMenu.Closed += ContextMenu_Closed;

                btn.ContextMenu.IsOpen = true;
            }
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
    }
}
