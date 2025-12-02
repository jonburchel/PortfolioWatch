using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PortfolioWatch
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _autoHideTimer;
        public bool IsPinned { get; set; }

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
                if (!IsPinned) _autoHideTimer.Start();
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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
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
            if (sender is FrameworkElement element && element.DataContext is ViewModels.MainViewModel.StockSearchResult result)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.SelectSearchResultCommand.Execute(result);
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
    }
}
