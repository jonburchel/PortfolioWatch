using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PortfolioWatch.ViewModels;

namespace PortfolioWatch.Views
{
    public class OpenEventArgs : EventArgs
    {
        public bool IsPinned { get; }
        public OpenEventArgs(bool isPinned)
        {
            IsPinned = isPinned;
        }
    }

    public partial class FloatingWindow : Window
    {
        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(FloatingWindow), new PropertyMetadata(false));

        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }

        public event EventHandler<OpenEventArgs>? OpenRequested;
        public event EventHandler? DragStarted;
        public event EventHandler? DragEnded;
        private DispatcherTimer _hoverTimer;
        public bool IsUserMoving { get; private set; }

        public FloatingWindow()
        {
            InitializeComponent();
            Loaded += FloatingWindow_Loaded;
            
            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(400);
            _hoverTimer.Tick += HoverTimer_Tick;
        }

        private void FloatingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure Window itself is fully opaque so Border opacity controls visibility
            this.Opacity = 1.0;

            if (MainBorder.ContextMenu != null)
            {
                MainBorder.ContextMenu.DataContext = this.DataContext;
            }
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            _hoverTimer.Start();
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoverTimer.Stop();
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();
            OpenRequested?.Invoke(this, new OpenEventArgs(false));
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _hoverTimer.Stop();

                var startLeft = this.Left;
                var startTop = this.Top;

                IsUserMoving = true;
                DragStarted?.Invoke(this, EventArgs.Empty);
                try
                {
                    this.DragMove();
                }
                finally
                {
                    IsUserMoving = false;
                    DragEnded?.Invoke(this, EventArgs.Empty);
                }

                // Only treat as a click if we didn't move significantly
                if (Math.Abs(this.Left - startLeft) < 2 && Math.Abs(this.Top - startTop) < 2)
                {
                    OpenRequested?.Invoke(this, new OpenEventArgs(true));
                }
            }
        }

        private void Border_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _hoverTimer.Stop();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow && mainWindow.DataContext is MainViewModel vm)
                {
                    await vm.Reset();
                    return;
                }
            }
        }
    }
}
