using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;
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
        private DispatcherTimer _topmostTimer;
        public bool IsUserMoving { get; private set; }

        public FloatingWindow()
        {
            InitializeComponent();
            Loaded += FloatingWindow_Loaded;
            
            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(400);
            _hoverTimer.Tick += HoverTimer_Tick;

            _topmostTimer = new DispatcherTimer();
            _topmostTimer.Interval = TimeSpan.FromSeconds(3);
            _topmostTimer.Tick += TopmostTimer_Tick;
            _topmostTimer.Start();
        }

        private void TopmostTimer_Tick(object? sender, EventArgs e)
        {
            // Discreetly enforce Topmost without stealing focus
            this.Topmost = true;
            
            // Use SetWindowPos to force it to the top of the Z-order
            // This helps if other topmost windows have covered it
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void FloatingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure Window itself is fully opaque so Border opacity controls visibility
            this.Opacity = 1.0;

            if (MainBorder.ContextMenu != null)
            {
                MainBorder.ContextMenu.DataContext = this.DataContext;
            }

            MainBorder.MouseMove += Border_MouseMove;
            MainBorder.MouseLeftButtonUp += Border_MouseLeftButtonUp;
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        private Point _dragStartMousePos;
        private Point _dragStartWindowPos;
        private bool _isMouseDown;

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _hoverTimer.Stop();
                _isMouseDown = true;
                
                Win32Point w32Mouse = new Win32Point();
                GetCursorPos(ref w32Mouse);
                _dragStartMousePos = new Point(w32Mouse.X, w32Mouse.Y);
                _dragStartWindowPos = new Point(this.Left, this.Top);

                ((UIElement)sender).CaptureMouse();
            }
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                Win32Point w32Mouse = new Win32Point();
                GetCursorPos(ref w32Mouse);
                Point currentMousePos = new Point(w32Mouse.X, w32Mouse.Y);

                double deltaX = currentMousePos.X - _dragStartMousePos.X;
                double deltaY = currentMousePos.Y - _dragStartMousePos.Y;

                if (!IsUserMoving)
                {
                    if (Math.Abs(deltaX) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(deltaY) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        IsUserMoving = true;
                        DragStarted?.Invoke(this, EventArgs.Empty);
                    }
                }

                if (IsUserMoving)
                {
                    this.Left = _dragStartWindowPos.X + deltaX;
                    this.Top = _dragStartWindowPos.Y + deltaY;
                }
            }
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMouseDown)
            {
                _isMouseDown = false;
                ((UIElement)sender).ReleaseMouseCapture();

                if (IsUserMoving)
                {
                    IsUserMoving = false;
                    DragEnded?.Invoke(this, EventArgs.Empty);
                }
                else
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
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow && mainWindow.DataContext is MainViewModel vm)
                    {
                        await vm.Reset();
                        App.CurrentApp.ResetWindowPositions();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                new ConfirmationWindow("Error", $"Error resetting settings: {ex.Message}", isAlert: true, icon: "❌").ShowDialog();
            }
        }
    }
}
