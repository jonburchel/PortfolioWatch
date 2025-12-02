using System;
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
        private DispatcherTimer _hoverTimer;

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
            this.Opacity = 0.3;
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Opacity = 1.0;
            _hoverTimer.Start();
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Opacity = 0.3;
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
                OpenRequested?.Invoke(this, new OpenEventArgs(true));
                this.DragMove();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow && mainWindow.DataContext is MainViewModel vm)
                {
                    vm.Reset();
                    return;
                }
            }
        }
    }
}
