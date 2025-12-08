using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

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

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            if (DataContext is ViewModels.MainViewModel vm)
            {
                var editingTab = vm.Tabs.FirstOrDefault(t => t.IsEditing);
                if (editingTab != null)
                {
                    var clickedElement = e.OriginalSource as DependencyObject;
                    bool isClickOnEditBox = false;

                    var current = clickedElement;
                    while (current != null)
                    {
                        if (current is System.Windows.Controls.TextBox tb && tb.DataContext == editingTab)
                        {
                            isClickOnEditBox = true;
                            break;
                        }

                        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                        {
                            current = VisualTreeHelper.GetParent(current);
                        }
                        else
                        {
                            current = LogicalTreeHelper.GetParent(current);
                        }
                    }

                    if (!isClickOnEditBox)
                    {
                        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox focusedTb &&
                            focusedTb.DataContext == editingTab)
                        {
                            focusedTb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                        }

                        editingTab.IsEditing = false;
                    }
                }
            }
        }

        private DispatcherTimer _autoHideTimer;
        private DispatcherTimer _tabScrollTimer;
        private int _scrollDirection = 0; // -1 left, 1 right
        private ScrollViewer? _headerScrollViewer;
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
            DataContextChanged += MainWindow_DataContextChanged;
            
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

            _tabScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _tabScrollTimer.Tick += TabScrollTimer_Tick;

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

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.MainViewModel oldVm)
            {
                oldVm.RequestSearchFocus -= ViewModel_RequestSearchFocus;
            }

            if (e.NewValue is ViewModels.MainViewModel newVm)
            {
                newVm.RequestSearchFocus += ViewModel_RequestSearchFocus;
            }
        }

        private void ViewModel_RequestSearchFocus(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                if (vm.NewSymbol == "Dow Jones")
                {
                    vm.NewSymbol = string.Empty;
                }
            }
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

        private void TabHeader_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is System.Windows.Controls.TextBox textBox)
                {
                    textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                    if (textBox.DataContext is ViewModels.PortfolioTabViewModel tabVm)
                    {
                        tabVm.IsEditing = false;
                    }
                }
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void TabHeader_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && 
                textBox.DataContext is ViewModels.PortfolioTabViewModel tabVm)
            {
                tabVm.IsEditing = false;
            }
        }

        private Point _startPoint;
        private bool _wasSelectedOnDown;
        private ViewModels.PortfolioTabViewModel? _draggedTab;
        private Helpers.DragAdorner? _dragAdorner;
        private bool _isDragging;

        private void TabItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is System.Windows.Controls.TabItem tabItem)
            {
                Point position = e.GetPosition(null);

                if (!_isDragging)
                {
                    if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        if (tabItem.DataContext is ViewModels.PortfolioTabViewModel tab && !tab.IsAddButton)
                        {
                            _isDragging = true;
                            _draggedTab = tab;

                            var adornerLayer = AdornerLayer.GetAdornerLayer(tabItem);
                            if (adornerLayer != null)
                            {
                                _dragAdorner = new Helpers.DragAdorner(tabItem, tabItem, 0.7);
                                adornerLayer.Add(_dragAdorner);
                            }

                            tabItem.CaptureMouse();
                        }
                    }
                }
                else
                {
                    double offsetX = position.X - _startPoint.X;
                    double offsetY = position.Y - _startPoint.Y;
                    _dragAdorner?.UpdatePosition(offsetX, offsetY);

                    var tabControl = ItemsControl.ItemsControlFromItemContainer(tabItem) as TabControl;
                    if (tabControl != null)
                    {
                        if (_headerScrollViewer == null)
                        {
                            _headerScrollViewer = tabControl.Template.FindName("HeaderScrollViewer", tabControl) as ScrollViewer;
                        }

                        if (_headerScrollViewer != null)
                        {
                            Point posInScrollViewer = e.GetPosition(_headerScrollViewer);
                            if (posInScrollViewer.X < 20)
                            {
                                _scrollDirection = -1;
                                if (!_tabScrollTimer.IsEnabled) _tabScrollTimer.Start();
                            }
                            else if (posInScrollViewer.X > _headerScrollViewer.ViewportWidth - 20)
                            {
                                _scrollDirection = 1;
                                if (!_tabScrollTimer.IsEnabled) _tabScrollTimer.Start();
                            }
                            else
                            {
                                _scrollDirection = 0;
                                _tabScrollTimer.Stop();
                            }
                        }

                        Point posInTabControl = e.GetPosition(tabControl);
                            VisualTreeHelper.HitTest(tabControl, null, (result) =>
                            {
                                var targetTabItem = FindVisualParent<System.Windows.Controls.TabItem>(result.VisualHit);
                                if (targetTabItem != null && targetTabItem != tabItem)
                                {
                                    if (targetTabItem.DataContext is ViewModels.PortfolioTabViewModel targetTab)
                                    {
                                        if (DataContext is ViewModels.MainViewModel vm)
                                        {
                                            int targetIndex = vm.Tabs.IndexOf(targetTab);
                                            int sourceIndex = vm.Tabs.IndexOf(_draggedTab!);
                                            if (targetIndex != -1 && sourceIndex != -1 && targetIndex != sourceIndex)
                                            {
                                                vm.MoveTab(_draggedTab!, targetIndex);
                                            }
                                        }
                                    }
                                }
                                return HitTestResultBehavior.Continue;
                            }, new PointHitTestParameters(posInTabControl));
                    }
                }
            }
        }

        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);

            if (sender is System.Windows.Controls.TabItem tabItem &&
                tabItem.DataContext is ViewModels.PortfolioTabViewModel)
            {
                _wasSelectedOnDown = tabItem.IsSelected;
            }
        }

        private void TabItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && sender is System.Windows.Controls.TabItem draggedItem)
            {
                _tabScrollTimer.Stop();
                draggedItem.ReleaseMouseCapture();
                if (_dragAdorner != null)
                {
                    AdornerLayer.GetAdornerLayer(draggedItem)?.Remove(_dragAdorner);
                    _dragAdorner = null;
                }
                _isDragging = false;
                _draggedTab = null;
                e.Handled = true;
                return;
            }

            if (sender is System.Windows.Controls.TabItem tabItem &&
                tabItem.DataContext is ViewModels.PortfolioTabViewModel tab &&
                !tab.IsAddButton)
            {
                Point position = e.GetPosition(null);
                bool isDrag = Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                              Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

                if (_wasSelectedOnDown && !isDrag)
                {
                    // Check if we clicked the delete button
                    if (e.OriginalSource is DependencyObject originalSource)
                    {
                        var parent = originalSource;
                        while (parent != null && parent != tabItem)
                        {
                            if (parent is System.Windows.Controls.Button)
                            {
                                return;
                            }
                            parent = VisualTreeHelper.GetParent(parent);
                        }
                    }

                    // Only enter edit mode if clicking the text block explicitly
                    if (!(e.OriginalSource is TextBlock))
                    {
                        return;
                    }

                    tab.IsEditing = true;

                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        var textBox = FindVisualChild<System.Windows.Controls.TextBox>(tabItem);
                        if (textBox != null)
                        {
                            textBox.Focus();
                            try
                            {
                                // Place caret at click position instead of selecting all
                                var point = e.GetPosition(textBox);
                                int index = textBox.GetCharacterIndexFromPoint(point, true);
                                textBox.CaretIndex = index;
                            }
                            catch
                            {
                                textBox.SelectAll();
                            }
                        }
                    }));
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
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
            if (actualWidth <= 0)
            {
                GraphTooltip.IsOpen = false;
                return;
            }

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

            if (selectedRange == "1d" || timestamps == null || timestamps.Count != history.Count || timestamps.Count == 0)
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
                
                if (double.IsNaN(hoverSeconds) || double.IsInfinity(hoverSeconds))
                {
                    GraphTooltip.IsOpen = false;
                    return;
                }

                DateTime hoverTime;
                try
                {
                    hoverTime = startTime.AddSeconds(hoverSeconds);
                }
                catch
                {
                    GraphTooltip.IsOpen = false;
                    return;
                }

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

        private void TaxPie_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && 
                element.DataContext is ViewModels.PortfolioTabViewModel tabVm)
            {
                Point point = element.PointToScreen(new Point(0, element.ActualHeight));
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var matrix = source.CompositionTarget.TransformFromDevice;
                    var logicalPoint = matrix.Transform(point);

                    var dialog = new Views.TaxStatusEditWindow(tabVm.TaxAllocations)
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = logicalPoint.X,
                        Top = logicalPoint.Y + 5
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        tabVm.TaxAllocations = new System.Collections.ObjectModel.ObservableCollection<Models.TaxAllocation>(dialog.ViewModel.GetAllocations());
                    }
                    e.Handled = true;
                }
            }
        }

        private void AggregateTaxPie_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.ToolTip is ToolTip toolTip)
                {
                    toolTip.DataContext = element.DataContext;
                    toolTip.PlacementTarget = element;
                    toolTip.IsOpen = true;
                    e.Handled = true;
                }
            }
        }

        private void AggregateTaxPie_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.ToolTip is ToolTip toolTip)
                {
                    toolTip.IsOpen = false;
                }
            }
        }

        private void PieChart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.ToolTip is ToolTip toolTip)
                {
                    toolTip.IsOpen = true;
                    e.Handled = true;
                }
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && DataContext is ViewModels.MainViewModel vm)
            {
                if (e.Delta > 0)
                    vm.UIScale = Math.Min(2.0, vm.UIScale + 0.1);
                else
                    vm.UIScale = Math.Max(0.5, vm.UIScale - 0.1);
                
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && DataContext is ViewModels.MainViewModel vm)
            {
                if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    vm.UIScale = Math.Min(2.0, vm.UIScale + 0.1);
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    vm.UIScale = Math.Max(0.5, vm.UIScale - 0.1);
                    e.Handled = true;
                }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                {
                    vm.UIScale = 1.0;
                    e.Handled = true;
                }
            }
        }

        private void TabScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (_headerScrollViewer != null && _scrollDirection != 0)
            {
                _headerScrollViewer.ScrollToHorizontalOffset(_headerScrollViewer.HorizontalOffset + (_scrollDirection * 5));
            }
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

        private void ScrollLeftButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RepeatButton btn && btn.TemplatedParent is TabControl tabControl)
            {
                var scrollViewer = tabControl.Template.FindName("HeaderScrollViewer", tabControl) as ScrollViewer;
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - 20);
                }
            }
        }

        private void ScrollRightButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RepeatButton btn && btn.TemplatedParent is TabControl tabControl)
            {
                var scrollViewer = tabControl.Template.FindName("HeaderScrollViewer", tabControl) as ScrollViewer;
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + 20);
                }
            }
        }

        private void HeaderScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer sv && sv.TemplatedParent is TabControl tabControl)
            {
                var leftBtn = tabControl.Template.FindName("ScrollLeftButton", tabControl) as RepeatButton;
                var rightBtn = tabControl.Template.FindName("ScrollRightButton", tabControl) as RepeatButton;

                if (leftBtn != null)
                {
                    leftBtn.Visibility = sv.HorizontalOffset > 0.5 ? Visibility.Visible : Visibility.Collapsed;
                }

                if (rightBtn != null)
                {
                    rightBtn.Visibility = sv.HorizontalOffset < (sv.ScrollableWidth - 0.5) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }
}
