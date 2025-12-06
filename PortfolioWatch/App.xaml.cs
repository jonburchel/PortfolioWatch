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
                MessageBox.Show($"Error initializing tray icon: {ex.Message}");
                // Fallback or continue
                _notifyIcon = new TaskbarIcon { ToolTipText = "Portfolio Watch" };
            }

            // Load Settings
            var settings = _settingsService.LoadSettings();

            // Apply Theme
            ApplyTheme(settings.Theme);

            // Initialize Windows
            _mainWindow = new MainWindow();
            _floatingWindow = new FloatingWindow();
            
            // Share DataContext
            _floatingWindow.DataContext = _mainWindow.DataContext;

            // Context Menu for Tray Icon
            var contextMenu = (System.Windows.Controls.ContextMenu)FindResource("SharedContextMenu");
            contextMenu.DataContext = _mainWindow.DataContext;
            _notifyIcon.ContextMenu = contextMenu;

            // Apply Settings
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
                // Lower edge 50px BELOW top of taskbar (WorkArea.Bottom)
                _floatingWindow.Top = desktopWorkingArea.Bottom + 50 - _floatingWindow.Height;
            }

            _intendedLeft = _floatingWindow.Left;
            _intendedTop = _floatingWindow.Top;
            
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
            _floatingWindow.DragStarted += FloatingWindow_DragStarted;
            _floatingWindow.DragEnded += FloatingWindow_DragEnded;
            
            // Ensure pinning state is reset when main window is hidden
            _mainWindow.IsVisibleChanged += (s, args) =>
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.IsPinned = false;
                    _floatingWindow.IsPinned = false;
                }
                else
                {
                    RefreshData();
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

            _mainWindow.LocationChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                
                if (_mainWindow.IsUserMoving)
                {
                    _isSyncing = true;

                    // Move FloatingWindow based on current relative orientation
                    if (_currentIsBelow)
                    {
                        // MW is Below. FW should be Above.
                        _floatingWindow.Top = _mainWindow.Top - _floatingWindow.ActualHeight + 20;
                    }
                    else
                    {
                        // MW is Above. FW should be Below.
                        _floatingWindow.Top = _mainWindow.Top - 20 + _mainWindow.Height;
                    }

                    if (_currentIsRight)
                    {
                        // MW is Right. FW should be Left.
                        _floatingWindow.Left = _mainWindow.Left - 20;
                    }
                    else
                    {
                        // MW is Left. FW should be Right.
                        _floatingWindow.Left = _mainWindow.Left + _mainWindow.Width + 20 - _floatingWindow.ActualWidth;
                    }

                    _intendedLeft = _floatingWindow.Left;
                    _intendedTop = _floatingWindow.Top;

                    _isSyncing = false;
                }
            };
            
            // Also sync when MainWindow resizes
            _mainWindow.SizeChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                
                _isSyncing = true;
                
                // When MainWindow resizes, we want to move the FloatingWindow to maintain the gap
                // instead of moving the MainWindow back to the old anchor.
                
                if (_currentIsBelow)
                {
                    // MainWindow is BELOW FloatingWindow.
                    // FloatingWindow should be 20px ABOVE MainWindow (Overlap by 20px to match "Below" behavior).
                    // FW.Bottom = MW.Top + 20
                    // FW.Top + FW.Height = MW.Top + 20
                    // FW.Top = MW.Top + 20 - FW.Height
                    _floatingWindow.Top = _mainWindow.Top + 20 - _floatingWindow.ActualHeight;
                }
                else
                {
                    // MainWindow is ABOVE FloatingWindow.
                    // FloatingWindow should be 20px BELOW MainWindow.
                    // FW.Top = MW.Top + MW.Height + 20
                    // Note: We use 20px gap. Previous logic used 20px overlap/offset?
                    // Let's check UpdateMainWindowPosition logic:
                    // if (!_currentIsBelow) _mainWindow.Top = fwTop + 20 - _mainWindow.Height;
                    // This implies fwTop = _mainWindow.Top + _mainWindow.Height - 20;
                    // So there is a 20px OVERLAP (or offset from bottom).
                    // The user said "stay the same number of pixels above or below".
                    // If the current logic establishes a -20px gap (overlap), we should maintain that.
                    
                    // Let's stick to the formula derived from UpdateMainWindowPosition to be consistent.
                    // fwTop = _mainWindow.Top + _mainWindow.Height - 20;
                    
                    _floatingWindow.Top = _mainWindow.Top + _mainWindow.Height - 20;
                }

                // We might also need to adjust Left if alignment depends on Width?
                // UpdateMainWindowPosition logic:
                // if (_currentIsRight) _mainWindow.Left = fwLeft + 20;
                // else _mainWindow.Left = fwLeft + fwWidth - _mainWindow.Width - 20;
                
                if (_currentIsRight)
                {
                    // Aligned Left edges.
                    // If MW Left changes (resizing left edge), we need to move FW.
                    // fwLeft = MW.Left - 20
                    _floatingWindow.Left = _mainWindow.Left - 20;
                }
                else
                {
                    // Aligned Right edges.
                    // If MW Width changes, we need to move FW?
                    // fwLeft = _mainWindow.Left + _mainWindow.Width + 20 - fwWidth
                    _floatingWindow.Left = _mainWindow.Left + _mainWindow.Width + 20 - _floatingWindow.ActualWidth;
                }

                _intendedLeft = _floatingWindow.Left;
                _intendedTop = _floatingWindow.Top;

                _isSyncing = false;
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
                
                // Position dynamically
                UpdateMainWindowPosition();

                _mainWindow.Show();
                _mainWindow.Activate();

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
            _mainWindow.Width = 800;
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

            var workArea = SystemParameters.WorkArea;
            var fwLeft = _floatingWindow.Left;
            var fwTop = _floatingWindow.Top;
            var fwWidth = _floatingWindow.ActualWidth;
            var fwHeight = _floatingWindow.ActualHeight;

            // Vertical
            var spaceAbove = fwTop - workArea.Top;
            var spaceBelow = workArea.Bottom - (fwTop + fwHeight);
            
            _currentIsBelow = spaceBelow > spaceAbove;

            if (_currentIsBelow)
            {
                // Position Below
                _mainWindow.Top = fwTop + fwHeight - 20;
            }
            else
            {
                // Position Above
                _mainWindow.Top = fwTop + 20 - _mainWindow.Height;
            }

            // Horizontal
            var spaceLeft = fwLeft - workArea.Left;
            var spaceRight = workArea.Right - (fwLeft + fwWidth);

            _currentIsRight = spaceRight > spaceLeft; // More space to the right -> Put window on right (Left aligned)

            if (_currentIsRight)
            {
                // Align Left edges (Window extends right)
                _mainWindow.Left = fwLeft + 20;
            }
            else
            {
                // Align Right edges (Window extends left)
                _mainWindow.Left = fwLeft + fwWidth - _mainWindow.Width - 20;
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
                // Execute on UI thread
                Dispatcher.Invoke(() => vm.RefreshCommand.Execute(null));
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
