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
        private SettingsService _settingsService;
        private bool _isSyncing;

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
                    _mainWindow.Left = _floatingWindow.Left + 20;
                    _mainWindow.Top = _floatingWindow.Top + 20 - _mainWindow.Height; // Removed gap
                }
                
                _isSyncing = false;
            };

            _mainWindow.LocationChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                _isSyncing = true;

                // Move FloatingWindow to stay below MainWindow
                _floatingWindow.Left = _mainWindow.Left - 20;
                _floatingWindow.Top = _mainWindow.Top + _mainWindow.Height - 20; // Removed gap

                _isSyncing = false;
            };
            
            // Also sync when MainWindow resizes (height changes)
            _mainWindow.SizeChanged += (s, args) =>
            {
                if (_isSyncing || _floatingWindow == null) return;
                if (args.HeightChanged)
                {
                    _isSyncing = true;
                    _mainWindow.Top = _floatingWindow.Top + 20 - _mainWindow.Height; // Removed gap
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
                _mainWindow.Left = _floatingWindow.Left + 20;
                _mainWindow.Top = _floatingWindow.Top + 20 - _mainWindow.Height; // Removed gap

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
                var settings = _settingsService.LoadSettings();
                if (settings.Theme == AppTheme.System)
                {
                    ApplyTheme(AppTheme.System);
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

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
