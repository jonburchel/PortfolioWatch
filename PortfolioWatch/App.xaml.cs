using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using PortfolioWatch.Services;
using PortfolioWatch.ViewModels;
using PortfolioWatch.Views;

namespace PortfolioWatch
{
    public partial class App : Application
    {
        private const string MutexName = "PortfolioWatch_Singleton_Mutex";
        private static Mutex? _mutex;

        private TaskbarIcon? _notifyIcon;
        private FloatingWindow? _floatingWindow;
        private MainWindow? _mainWindow;
        private SettingsService _settingsService;
        private bool _isSyncing;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public App()
        {
            _settingsService = new SettingsService();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // App is already running!
                Process current = Process.GetCurrentProcess();
                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        SetForegroundWindow(process.MainWindowHandle);
                        break;
                    }
                }
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                // Initialize Tray Icon
                _notifyIcon = new TaskbarIcon
                {
                    ToolTipText = "Portfolio Watch",
                    IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/pyramid.png"))
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing tray icon: {ex.Message}");
                // Fallback or continue
                _notifyIcon = new TaskbarIcon { ToolTipText = "Portfolio Watch" };
            }
            
            // Context Menu for Tray Icon
            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            var resetItem = new System.Windows.Controls.MenuItem { Header = "Reset" };
            resetItem.Click += (s, args) => 
            {
                if (_mainWindow?.DataContext is MainViewModel vm)
                {
                    vm.Reset();
                }
            };
            contextMenu.Items.Add(resetItem);
            
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (s, args) => Shutdown();
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;

            // Load Settings
            var settings = _settingsService.LoadSettings();

            // Initialize Windows
            _mainWindow = new MainWindow();
            _floatingWindow = new FloatingWindow();
            
            // Share DataContext
            _floatingWindow.DataContext = _mainWindow.DataContext;

            // Apply Settings
            if (settings.WindowLeft != 0 && settings.WindowTop != 0)
            {
                _floatingWindow.Left = settings.WindowLeft;
                _floatingWindow.Top = settings.WindowTop;
            }
            else
            {
                // Default position: Bottom Left
                var desktopWorkingArea = SystemParameters.WorkArea;
                _floatingWindow.Left = 20;
                _floatingWindow.Top = desktopWorkingArea.Bottom - _floatingWindow.Height - 20;
            }
            
            if (settings.WindowHeight > 0)
            {
                _mainWindow.Height = settings.WindowHeight;
            }
            if (settings.WindowWidth > 0)
            {
                _mainWindow.Width = settings.WindowWidth;
            }

            // Wire up events
            _floatingWindow.OpenRequested += FloatingWindow_OpenRequested;
            
            // Ensure pinning state is reset when main window is hidden
            _mainWindow.IsVisibleChanged += (s, args) =>
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.IsPinned = false;
                    _floatingWindow.IsPinned = false;
                }
            };
            _notifyIcon.TrayLeftMouseUp += (s, args) => ShowMainWindow(true);

            // Handle FloatingWindow MouseLeave to trigger MainWindow auto-hide
            _floatingWindow.MouseLeave += (s, args) => 
            {
                if (_mainWindow != null && _mainWindow.IsVisible && !_mainWindow.IsPinned && !_mainWindow.IsMouseOver)
                {
                    _mainWindow.StartAutoHide();
                }
            };

            // Window Syncing
            _floatingWindow.LocationChanged += (s, args) =>
            {
                if (_isSyncing || _mainWindow == null) return;
                _isSyncing = true;
                
                // Move MainWindow to stay above FloatingWindow
                if (_mainWindow.IsVisible)
                {
                    _mainWindow.Left = _floatingWindow.Left;
                    _mainWindow.Top = _floatingWindow.Top - _mainWindow.Height; // Removed gap
                }
                
                _isSyncing = false;
            };

            _mainWindow.LocationChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                _isSyncing = true;

                // Move FloatingWindow to stay below MainWindow
                _floatingWindow.Left = _mainWindow.Left;
                _floatingWindow.Top = _mainWindow.Top + _mainWindow.Height; // Removed gap

                _isSyncing = false;
            };
            
            // Also sync when MainWindow resizes (height changes)
            _mainWindow.SizeChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                if (args.HeightChanged)
                {
                    _isSyncing = true;
                    _mainWindow.Top = _floatingWindow.Top - _mainWindow.Height; // Removed gap
                    _isSyncing = false;
                }
            };

            // Show Floating Window
            try 
            {
                _floatingWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing floating window: {ex.Message}");
            }

            // Ensure Start with Windows
            if (settings.StartWithWindows)
            {
                _settingsService.SetStartup(true);
            }
        }

        private void FloatingWindow_OpenRequested(object? sender, OpenEventArgs e)
        {
            ShowMainWindow(e.IsPinned);
        }

        private void ShowMainWindow(bool isPinned)
        {
            if (_mainWindow == null || _floatingWindow == null) return;

            // If already visible and we are clicking (pinning), toggle if it was already pinned?
            // Or if it was transient, make it pinned.
            if (_mainWindow.IsVisible)
            {
                if (isPinned)
                {
                    if (_mainWindow.IsPinned)
                    {
                        // Was pinned, click again -> Hide
                        _mainWindow.Hide();
                        _mainWindow.IsPinned = false;
                        _floatingWindow.IsPinned = false;
                    }
                    else
                    {
                        // Was transient, click -> Pin
                        _mainWindow.IsPinned = true;
                        _floatingWindow.IsPinned = true;
                        _mainWindow.CancelAutoHide();
                        _mainWindow.ShowPinningTooltip();
                    }
                }
                else
                {
                    // Hover while visible.
                    // If pinned, do nothing.
                    // If transient, ensure auto-hide is cancelled while hovering floating window
                    if (!_mainWindow.IsPinned)
                    {
                        _mainWindow.CancelAutoHide();
                    }
                }
            }
            else
            {
                // Not visible -> Show
                _mainWindow.IsPinned = isPinned;
                _floatingWindow.IsPinned = isPinned;
                
                // Position above floating window
                _mainWindow.Left = _floatingWindow.Left;
                _mainWindow.Top = _floatingWindow.Top - _mainWindow.Height; // Removed gap

                _mainWindow.Show();
                _mainWindow.Activate();

                if (isPinned)
                {
                    _mainWindow.ShowPinningTooltip();
                }
                // If not pinned (transient), do NOT start auto-hide yet. 
                // It will be started when the mouse leaves the FloatingWindow.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_floatingWindow != null && _mainWindow != null)
            {
                // Reload settings to ensure we have the latest stocks saved by MainViewModel
                var settings = _settingsService.LoadSettings();
                settings.WindowLeft = _floatingWindow.Left;
                settings.WindowTop = _floatingWindow.Top;
                settings.WindowHeight = _mainWindow.Height;
                settings.WindowWidth = _mainWindow.Width;
                _settingsService.SaveSettings(settings);
            }

            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

    }
}
