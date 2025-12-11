using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using PortfolioWatch.Models;
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
        private DragPreviewWindow? _dragPreviewWindow;
        private SettingsService _settingsService;
        private bool _isSyncing;
        private double _intendedLeft;
        private double _intendedTop;
        private bool _currentIsBelow;
        private bool _currentIsRight;

        public static App CurrentApp => (App)Current;
        public FloatingWindow? FloatingWindow => _floatingWindow;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public App()
        {
            _settingsService = new SettingsService();
            
            // Global exception handling
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception, "Dispatcher");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException(e.ExceptionObject as Exception, "AppDomain");
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception, "TaskScheduler");
            e.SetObserved();
        }

        private void HandleException(Exception? ex, string source)
        {
            if (ex == null) return;
            
            try
            {
                string message = $"An unexpected error occurred ({source}):\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                MessageBox.Show(message, "Portfolio Watch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last resort
                Debug.WriteLine($"CRITICAL ERROR ({source}): {ex}");
            }
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

            // Register file association
            RegisterFileAssociation();

            // Check for startup file argument
            string? startupFile = null;
            if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
            {
                startupFile = e.Args[0];
            }

            // System Theme Change Listener
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

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
                new ConfirmationWindow("Error", $"Error initializing tray icon: {ex.Message}", isAlert: true, icon: "❌").ShowDialog();
                // Fallback or continue
                _notifyIcon = new TaskbarIcon { ToolTipText = "Portfolio Watch" };
            }

            // Load Settings
            var settings = _settingsService.LoadSettings();

            // Apply Theme
            ApplyTheme(settings.Theme);

            // Create VM explicitly
            var vm = new MainViewModel();

            // Initialize Floating Window FIRST
            _floatingWindow = new FloatingWindow();
            _floatingWindow.DataContext = vm;

            // Apply Settings to Floating Window
            if (settings.WindowLeft != 0 && settings.WindowTop != 0)
            {
                _floatingWindow.Left = settings.WindowLeft;
                _floatingWindow.Top = settings.WindowTop;
            }
            else
            {
                // Default position: Bottom Left, slightly overlapping taskbar
                var desktopWorkingArea = SystemParameters.WorkArea;
                _floatingWindow.Left = -10;
                
                // Ensure Height is valid
                double fwHeight = _floatingWindow.Height;
                if (double.IsNaN(fwHeight)) fwHeight = 100; // Default from XAML

                // Lower edge 50px BELOW top of taskbar (WorkArea.Bottom)
                _floatingWindow.Top = desktopWorkingArea.Bottom + 50 - fwHeight;
            }

            _intendedLeft = _floatingWindow.Left;
            _intendedTop = _floatingWindow.Top;

            // Wire up Floating Window events that don't depend on MainWindow yet
            _floatingWindow.OpenRequested += FloatingWindow_OpenRequested;
            _floatingWindow.DragStarted += FloatingWindow_DragStarted;
            _floatingWindow.DragEnded += FloatingWindow_DragEnded;
            
            // Handle FloatingWindow MouseLeave to trigger MainWindow auto-hide
            _floatingWindow.MouseLeave += (s, args) => 
            {
                if (_mainWindow != null && _mainWindow.IsVisible && !_mainWindow.IsPinned && !_mainWindow.IsMouseOver)
                {
                    _mainWindow.StartAutoHide();
                }
            };

            // Window Syncing (Floating Window side)
            _floatingWindow.LocationChanged += (s, args) =>
            {
                if (_isSyncing || _mainWindow == null) return;

                // Sticky positioning logic
                if (_floatingWindow.IsUserMoving)
                {
                    _intendedLeft = _floatingWindow.Left;
                    _intendedTop = _floatingWindow.Top;
                    UpdateDragPreviewPosition();
                }
                else
                {
                    // If moved by system (not user), revert to intended position
                    if (Math.Abs(_floatingWindow.Left - _intendedLeft) > 1 || Math.Abs(_floatingWindow.Top - _intendedTop) > 1)
                    {
                        _floatingWindow.Left = _intendedLeft;
                        _floatingWindow.Top = _intendedTop;
                        return; // Don't sync if we reverted
                    }
                }

                _isSyncing = true;
                
                // Move MainWindow based on dynamic positioning
                if (_mainWindow.IsVisible)
                {
                    UpdateMainWindowPosition();
                }
                
                _isSyncing = false;
            };

            // Show Floating Window immediately
            try 
            {
                _floatingWindow.Show();
            }
            catch (Exception ex)
            {
                new ConfirmationWindow("Error", $"Error showing floating window: {ex.Message}", isAlert: true, icon: "❌").ShowDialog();
            }

            // Defer MainWindow creation and heavy initialization
            Dispatcher.InvokeAsync(() => 
            {
                InitializeMainWindow(settings, vm);
                
                if (!string.IsNullOrEmpty(startupFile))
                {
                    // If we have a startup file, import it
                    // We do this after initialization so the UI is ready
                    vm.ImportPortfolio(startupFile);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RegisterFileAssociation()
        {
            try
            {
                string extension = ".pwatch";
                string progId = "PortfolioWatchFile";
                string description = "Portfolio Watch File";
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

                if (string.IsNullOrEmpty(exePath)) return;

                // Register extension
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
                {
                    if (key.GetValue(null) as string != progId)
                    {
                        key.SetValue(null, progId);
                    }
                }

                // Register ProgId
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    if (key.GetValue(null) as string != description)
                    {
                        key.SetValue(null, description);
                    }

                    using (var iconKey = key.CreateSubKey("DefaultIcon"))
                    {
                        string iconValue = $"\"{exePath}\",0";
                        if (iconKey.GetValue(null) as string != iconValue)
                        {
                            iconKey.SetValue(null, iconValue);
                        }
                    }

                    using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                    {
                        string commandValue = $"\"{exePath}\" \"%1\"";
                        if (commandKey.GetValue(null) as string != commandValue)
                        {
                            commandKey.SetValue(null, commandValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register file association: {ex.Message}");
            }
        }

        private void InitializeMainWindow(AppSettings settings, MainViewModel vm)
        {
            _mainWindow = new MainWindow();
            _mainWindow.DataContext = vm;

            // Context Menu for Tray Icon
            var contextMenu = (System.Windows.Controls.ContextMenu)FindResource("SharedContextMenu");
            contextMenu.DataContext = vm;
            if (_notifyIcon != null) _notifyIcon.ContextMenu = contextMenu;

            // Apply Settings to MainWindow
            if (settings.WindowHeight > 0) _mainWindow.Height = settings.WindowHeight;
            if (settings.WindowWidth > 0) _mainWindow.Width = settings.WindowWidth;

            // Wire up remaining events
            vm.RequestShowAndPin += (s, args) => ShowMainWindow(true, forcePin: true);

            _mainWindow.IsVisibleChanged += (s, args) =>
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.IsPinned = false;
                    if (_floatingWindow != null) _floatingWindow.IsPinned = false;
                }
                else
                {
                    RefreshData();
                }
            };

            if (_notifyIcon != null) _notifyIcon.TrayLeftMouseUp += (s, args) => ShowMainWindow(true);

            // Window Syncing (MainWindow side)
            _mainWindow.LocationChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                
                if (_mainWindow.IsUserMoving)
                {
                    _isSyncing = true;

                    // Move FloatingWindow based on current relative orientation
                    if (_currentIsBelow)
                    {
                        _floatingWindow.Top = _mainWindow.Top - _floatingWindow.ActualHeight + 20;
                    }
                    else
                    {
                        _floatingWindow.Top = _mainWindow.Top - 20 + _mainWindow.Height;
                    }

                    if (_currentIsRight)
                    {
                        _floatingWindow.Left = _mainWindow.Left - 20;
                    }
                    else
                    {
                        _floatingWindow.Left = _mainWindow.Left + _mainWindow.Width + 20 - _floatingWindow.ActualWidth;
                    }

                    _intendedLeft = _floatingWindow.Left;
                    _intendedTop = _floatingWindow.Top;

                    _isSyncing = false;
                }
            };
            
            _mainWindow.SizeChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                
                _isSyncing = true;
                
                if (_currentIsBelow)
                {
                    _floatingWindow.Top = _mainWindow.Top + 20 - _floatingWindow.ActualHeight;
                }
                else
                {
                    _floatingWindow.Top = _mainWindow.Top + _mainWindow.Height - 20;
                }

                if (_currentIsRight)
                {
                    _floatingWindow.Left = _mainWindow.Left - 20;
                }
                else
                {
                    _floatingWindow.Left = _mainWindow.Left + _mainWindow.Width + 20 - _floatingWindow.ActualWidth;
                }

                _intendedLeft = _floatingWindow.Left;
                _intendedTop = _floatingWindow.Top;

                _isSyncing = false;
            };

            // Initialize VM data
            vm.Initialize();

            // Ensure Start with Windows
            if (settings.StartWithWindows)
            {
                _settingsService.SetStartup(true);
            }
        }

        private void FloatingWindow_DragStarted(object? sender, EventArgs e)
        {
            if (_mainWindow == null) return;

            _dragPreviewWindow = new DragPreviewWindow
            {
                Width = _mainWindow.Width,
                Height = _mainWindow.Height
            };
            
            UpdateDragPreviewPosition();
            _dragPreviewWindow.Show();
        }

        private void FloatingWindow_DragEnded(object? sender, EventArgs e)
        {
            if (_dragPreviewWindow != null)
            {
                _dragPreviewWindow.Close();
                _dragPreviewWindow = null;
            }
        }

        private void UpdateDragPreviewPosition()
        {
            if (_dragPreviewWindow == null || _floatingWindow == null || _mainWindow == null) return;

            // Use the same logic as UpdateMainWindowPosition but apply to preview window
            var workArea = SystemParameters.WorkArea;
            var fwLeft = _floatingWindow.Left;
            var fwTop = _floatingWindow.Top;
            var fwWidth = _floatingWindow.ActualWidth;
            var fwHeight = _floatingWindow.ActualHeight;

            // Vertical
            var spaceAbove = fwTop - workArea.Top;
            var spaceBelow = workArea.Bottom - (fwTop + fwHeight);
            
            bool isBelow = spaceBelow > spaceAbove;

            if (isBelow)
            {
                _dragPreviewWindow.Top = fwTop + fwHeight - 20;
            }
            else
            {
                _dragPreviewWindow.Top = fwTop + 20 - _mainWindow.Height;
            }

            // Horizontal
            var spaceLeft = fwLeft - workArea.Left;
            var spaceRight = workArea.Right - (fwLeft + fwWidth);

            bool isRight = spaceRight > spaceLeft;

            if (isRight)
            {
                _dragPreviewWindow.Left = fwLeft + 20;
            }
            else
            {
                _dragPreviewWindow.Left = fwLeft + fwWidth - _mainWindow.Width - 20;
            }
        }

        private void FloatingWindow_OpenRequested(object? sender, OpenEventArgs e)
        {
            ShowMainWindow(e.IsPinned);
        }

        private void ShowMainWindow(bool isPinned, bool forcePin = false)
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
                        if (forcePin) return; // Already pinned, do nothing

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
                
                // Position dynamically
                UpdateMainWindowPosition();

                _mainWindow.Show();
                
                if (isPinned)
                {
                    _mainWindow.Activate();
                }

                // Update again after show to ensure dimensions are correct if they were missing
                UpdateMainWindowPosition();

                if (isPinned)
                {
                    _mainWindow.ShowPinningTooltip();
                }
                // If not pinned (transient), do NOT start auto-hide yet. 
                // It will be started when the mouse leaves the FloatingWindow.
            }
        }

        public void ResetWindowPositions()
        {
            if (_mainWindow == null || _floatingWindow == null) return;

            _isSyncing = true;

            // Reset Main Window Size
            _mainWindow.Width = 450;
            _mainWindow.Height = 600;
            _mainWindow.UpdateLayout();

            // Reset Floating Window Position
            // Lower left corner, -10px from left edge.
            // Lower edge of button 50px BELOW top of taskbar.
            var workArea = SystemParameters.WorkArea;
            
            _floatingWindow.Left = -9;
            _floatingWindow.Top = workArea.Bottom + 34 - _floatingWindow.ActualHeight;

            // Update intended position to prevent reversion
            _intendedLeft = _floatingWindow.Left;
            _intendedTop = _floatingWindow.Top;

            // Force update of relative orientation flags
            UpdateMainWindowPosition();

            _isSyncing = false;
        }

        private void UpdateMainWindowPosition()
        {
            if (_mainWindow == null || _floatingWindow == null) return;

            try
            {
                var workArea = SystemParameters.WorkArea;
                var fwLeft = _floatingWindow.Left;
                var fwTop = _floatingWindow.Top;
                var fwWidth = _floatingWindow.ActualWidth;
                var fwHeight = _floatingWindow.ActualHeight;

                // Defensive checks for NaN
                if (double.IsNaN(fwLeft)) fwLeft = 0;
                if (double.IsNaN(fwTop)) fwTop = 0;
                if (fwWidth <= 0) fwWidth = 100; // Fallback
                if (fwHeight <= 0) fwHeight = 100; // Fallback

                // Vertical
                var spaceAbove = fwTop - workArea.Top;
                var spaceBelow = workArea.Bottom - (fwTop + fwHeight);
                
                _currentIsBelow = spaceBelow > spaceAbove;

                double mwHeight = _mainWindow.ActualHeight;
                if (mwHeight <= 0) mwHeight = _mainWindow.Height;
                if (double.IsNaN(mwHeight)) mwHeight = 600; // Fallback

                if (_currentIsBelow)
                {
                    // Position Below
                    _mainWindow.Top = fwTop + fwHeight - 20;
                }
                else
                {
                    // Position Above
                    _mainWindow.Top = fwTop + 20 - mwHeight;
                }

                // Horizontal
                var spaceLeft = fwLeft - workArea.Left;
                var spaceRight = workArea.Right - (fwLeft + fwWidth);

                _currentIsRight = spaceRight > spaceLeft; // More space to the right -> Put window on right (Left aligned)

                double mwWidth = _mainWindow.ActualWidth;
                if (mwWidth <= 0) mwWidth = _mainWindow.Width;
                if (double.IsNaN(mwWidth)) mwWidth = 450; // Fallback

                if (_currentIsRight)
                {
                    // Align Left edges (Window extends right)
                    _mainWindow.Left = fwLeft + 20;
                }
                else
                {
                    // Align Right edges (Window extends left)
                    _mainWindow.Left = fwLeft + fwWidth - mwWidth - 20;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating main window position: {ex.Message}");
            }
        }

        public void ApplyTheme(AppTheme theme)
        {
            ResourceDictionary themeDictionary = new ResourceDictionary();
            string themeUri;

            switch (theme)
            {
                case AppTheme.Light:
                    themeUri = "Themes/LightTheme.xaml";
                    break;
                case AppTheme.Dark:
                    themeUri = "Themes/DarkTheme.xaml";
                    break;
                case AppTheme.System:
                default:
                    themeUri = IsSystemDarkTheme() ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
                    break;
            }

            try
            {
                themeDictionary.Source = new Uri(themeUri, UriKind.Relative);

                // Update application resources
                // We expect the theme dictionary to be the first merged dictionary (index 0)
                if (Resources.MergedDictionaries.Count > 0)
                {
                    Resources.MergedDictionaries[0] = themeDictionary;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply theme: {ex.Message}");
            }
        }

        private bool IsSystemDarkTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    return value is int i && i == 0;
                }
            }
            catch
            {
                return true; // Default to dark if check fails
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                Dispatcher.Invoke(() =>
                {
                    var settings = _settingsService.LoadSettings();
                    if (settings.Theme == AppTheme.System)
                    {
                        ApplyTheme(AppTheme.System);
                    }
                });
            }
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                RefreshData();
            }
        }

        private void RefreshData()
        {
            if (_mainWindow?.DataContext is MainViewModel vm)
            {
                // Execute on UI thread asynchronously to prevent deadlocks with SystemEvents
                Dispatcher.InvokeAsync(() => vm.RefreshCommand.Execute(null));
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

            if (_floatingWindow != null && _mainWindow != null && _mainWindow.DataContext is MainViewModel vm)
            {
                vm.SaveWindowPositions(
                    _floatingWindow.Left,
                    _floatingWindow.Top,
                    _mainWindow.Width,
                    _mainWindow.Height);
            }

            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

    }
}
