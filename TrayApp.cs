using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace TaskbarAutoHideOnResume
{
    public class TrayApp : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool autoHideEnabled;
        private Icon taskbarUpIcon;
        private Icon taskbarDownIcon;
        private string taskbarUpPath = "icons/taskbar-up.png";
        private string taskbarDownPath = "icons/taskbar-down.png";
        
        // Dual monitor support
        private System.Windows.Forms.Timer displayChangeTimer;
        private int lastDisplayCount;
        private string lastDisplayConfiguration;
        private bool isHandlingDisplayChange = false;

        public TrayApp()
        {
            try
            {
                LogDebug("TrayApp starting...");

                // Load PNG icons directly
                taskbarUpIcon = LoadIconFromPng(taskbarUpPath);
                taskbarDownIcon = LoadIconFromPng(taskbarDownPath);

                LogDebug($"Icons loaded - Up: {(taskbarUpIcon != null ? "Success" : "Failed")}, Down: {(taskbarDownIcon != null ? "Success" : "Failed")}");

                // Read the current auto-hide state from registry
                autoHideEnabled = GetTaskbarAutoHide();
                LogDebug($"Auto-hide state: {autoHideEnabled}");

                trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("Toggle Auto-Hide", null, ToggleAutoHide);
                trayMenu.Items.Add("Add to Startup", null, AddToStartup);
                trayMenu.Items.Add("Remove from Startup", null, RemoveFromStartup);
                trayMenu.Items.Add("Exit", null, OnExit);

                LogDebug("Context menu created");

                trayIcon = new NotifyIcon();
                trayIcon.Text = "Taskbar Killer";
                trayIcon.Icon = autoHideEnabled ? taskbarDownIcon : taskbarUpIcon;
                trayIcon.ContextMenuStrip = trayMenu;
                trayIcon.Visible = true;
                trayIcon.DoubleClick += ToggleAutoHide;
                trayIcon.MouseClick += TrayIcon_MouseClick;

                LogDebug($"Tray icon created - Visible: {trayIcon.Visible}, Icon: {(trayIcon.Icon != null ? "Set" : "Null")}, Text: {trayIcon.Text}");

                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                SystemEvents.SessionSwitch += OnSessionSwitch;
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

                // Initialize dual monitor support
                InitializeDualMonitorSupport();

                LogDebug("TrayApp initialization complete");
            }
            catch (Exception ex)
            {
                LogDebug($"ERROR: {ex.Message}\nStack trace: {ex.StackTrace}");
                MessageBox.Show($"Error initializing application: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitThread();
            }
        }

        private void LogDebug(string message)
        {
            try
            {
                string logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "debug.log");
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n";
                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private void ToggleAutoHide(object sender, EventArgs e)
        {
            ToggleTaskbarAutoHide();
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleTaskbarAutoHide();
            }
        }

        private void ToggleTaskbarAutoHide()
        {
            bool currentState = GetTaskbarAutoHide();
            bool newState = !currentState;
            SetTaskbarAutoHide(newState);
            trayIcon.Icon = newState ? taskbarDownIcon : taskbarUpIcon;
            autoHideEnabled = newState;
        }

        private void OnExit(object sender, EventArgs e)
        {
            LogDebug("Exit requested");
            trayIcon.Visible = false;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            
            // Cleanup dual monitor support
            if (displayChangeTimer != null)
            {
                displayChangeTimer.Stop();
                displayChangeTimer.Dispose();
            }
            
            ExitThread();
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume && autoHideEnabled)
            {
                SetTaskbarAutoHide(true);
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock && autoHideEnabled)
            {
                SetTaskbarAutoHide(true);
            }
        }

        private bool SetTaskbarAutoHide(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StuckRects3", true))
                {
                    if (key != null)
                    {
                        byte[] value = (byte[])key.GetValue("Settings");
                        if (value != null && value.Length >= 9)
                        {
                            if (enable)
                                value[8] |= 0x08;
                            else
                                value[8] &= unchecked((byte)~0x08);
                            key.SetValue("Settings", value, RegistryValueKind.Binary);
                            ApplyTaskbarAutoHide(enable);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing registry: {ex.Message}", "Registry Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }

        // Use SHAppBarMessage to apply the change instantly
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        private void ApplyTaskbarAutoHide(bool enable)
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(APPBARDATA));
            abd.hWnd = FindWindow("Shell_TrayWnd", null);
            abd.lParam = enable ? 1 : 0; // ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2
            const uint ABM_SETSTATE = 0x0000000a;
            SHAppBarMessage(ABM_SETSTATE, ref abd);
            RefreshTaskbar();
        }

        private bool GetTaskbarAutoHide()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StuckRects3", false))
                {
                    if (key != null)
                    {
                        byte[] value = (byte[])key.GetValue("Settings");
                        if (value != null && value.Length >= 9)
                        {
                            return (value[8] & 0x08) != 0;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail and return default state
            }
            return false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private void RefreshTaskbar()
        {
            IntPtr taskbarWnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarWnd != IntPtr.Zero)
            {
                const uint WM_SETTINGCHANGE = 0x001A;
                SendMessage(taskbarWnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private Icon LoadIconFromPng(string path)
        {
            try
            {
                // SIMPLIFIED: Just use the .ico file directly
                string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string icoPath = System.IO.Path.Combine(exeDir, "taskbar.ico");
                
                LogDebug($"Trying to load icon from: {icoPath}");
                LogDebug($"File exists: {System.IO.File.Exists(icoPath)}");
                
                if (System.IO.File.Exists(icoPath))
                {
                    Icon icon = new Icon(icoPath);
                    LogDebug($"Icon loaded successfully: {icon != null}");
                    return icon;
                }
                else
                {
                    LogDebug("Using SystemIcons.Information as fallback");
                    return SystemIcons.Information; // Use a visible system icon
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading icon: {ex.Message}");
                return SystemIcons.Error; // Use error icon so we can see SOMETHING
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // Call this method to create a startup shortcut
        private void CreateStartupShortcut()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = System.IO.Path.Combine(startupFolder, "TaskbarAutoHideOnResume.lnk");
            string exePath = Application.ExecutablePath;

            // Use Windows Script Host to create the shortcut
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(exePath);
            shortcut.Save();
        }

        // Call this method to remove the startup shortcut
        private void RemoveStartupShortcut()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = System.IO.Path.Combine(startupFolder, "TaskbarAutoHideOnResume.lnk");
            if (System.IO.File.Exists(shortcutPath))
            {
                System.IO.File.Delete(shortcutPath);
            }
        }

        private void AddToStartup(object sender, EventArgs e)
        {
            CreateStartupShortcut();
            MessageBox.Show("Startup shortcut created.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RemoveFromStartup(object sender, EventArgs e)
        {
            RemoveStartupShortcut();
            MessageBox.Show("Startup shortcut removed.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #region Dual Monitor Support

        // Additional Windows API declarations for dual monitor support
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITORINFOF_PRIMARY = 0x00000001;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint ABM_GETSTATE = 0x00000004;
        private const uint ABM_SETSTATE = 0x0000000a;
        private const uint ABS_AUTOHIDE = 0x0000001;
        private const uint ABS_ALWAYSONTOP = 0x0000002;

        private void InitializeDualMonitorSupport()
        {
            try
            {
                LogDebug("Initializing dual monitor support...");
                
                // Get initial display configuration
                lastDisplayCount = Screen.AllScreens.Length;
                lastDisplayConfiguration = GetDisplayConfiguration();
                
                LogDebug($"Initial display count: {lastDisplayCount}");
                LogDebug($"Initial display config: {lastDisplayConfiguration}");

                // Set up timer for periodic monitoring (backup to event-based monitoring)
                displayChangeTimer = new System.Windows.Forms.Timer();
                displayChangeTimer.Interval = 2000; // Check every 2 seconds
                displayChangeTimer.Tick += OnDisplayChangeTimer;
                displayChangeTimer.Start();

                LogDebug("Dual monitor support initialized successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"Error initializing dual monitor support: {ex.Message}");
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            LogDebug("Display settings changed event triggered");
            HandleDisplayChange();
        }

        private void OnDisplayChangeTimer(object sender, EventArgs e)
        {
            try
            {
                int currentDisplayCount = Screen.AllScreens.Length;
                string currentDisplayConfig = GetDisplayConfiguration();

                if (currentDisplayCount != lastDisplayCount || currentDisplayConfig != lastDisplayConfiguration)
                {
                    LogDebug($"Display change detected - Count: {lastDisplayCount} -> {currentDisplayCount}");
                    LogDebug($"Config change: {lastDisplayConfiguration} -> {currentDisplayConfig}");
                    
                    lastDisplayCount = currentDisplayCount;
                    lastDisplayConfiguration = currentDisplayConfig;
                    
                    HandleDisplayChange();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in display change timer: {ex.Message}");
            }
        }

        private string GetDisplayConfiguration()
        {
            try
            {
                var screens = Screen.AllScreens;
                var config = string.Join("|", screens.Select(s => $"{s.Bounds.Width}x{s.Bounds.Height}@{s.Bounds.X},{s.Bounds.Y}"));
                return config;
            }
            catch
            {
                return "unknown";
            }
        }

        private async void HandleDisplayChange()
        {
            if (isHandlingDisplayChange)
            {
                LogDebug("Already handling display change, skipping...");
                return;
            }

            try
            {
                isHandlingDisplayChange = true;
                LogDebug("Handling display change...");

                // Wait a moment for the display change to settle
                await Task.Delay(1000);

                // Check for and resolve taskbar conflicts
                await ResolveTaskbarConflicts();

                // Restore auto-hide setting if it was enabled
                if (autoHideEnabled)
                {
                    LogDebug("Restoring auto-hide setting after display change");
                    await Task.Delay(500); // Additional delay before applying settings
                    SetTaskbarAutoHide(true);
                    trayIcon.Icon = taskbarDownIcon;
                }

                LogDebug("Display change handling completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling display change: {ex.Message}");
            }
            finally
            {
                isHandlingDisplayChange = false;
            }
        }

        private async Task ResolveTaskbarConflicts()
        {
            try
            {
                LogDebug("Checking for taskbar conflicts...");

                // Look for duplicate taskbars or error dialogs
                await CloseTaskbarErrorDialogs();
                
                // Reset taskbar settings to resolve conflicts
                await ResetTaskbarSettings();
                
                // Force refresh of all taskbars
                RefreshAllTaskbars();

                LogDebug("Taskbar conflict resolution completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error resolving taskbar conflicts: {ex.Message}");
            }
        }

        private async Task CloseTaskbarErrorDialogs()
        {
            try
            {
                LogDebug("Looking for taskbar error dialogs...");
                
                // Look for common error dialog patterns
                string[] errorTitles = {
                    "Taskbar",
                    "Windows Shell",
                    "Explorer",
                    "can't have 2 taskbars",
                    "taskbar error"
                };

                foreach (string title in errorTitles)
                {
                    IntPtr errorDialog = FindWindow(null, title);
                    if (errorDialog != IntPtr.Zero)
                    {
                        LogDebug($"Found error dialog: {title}");
                        SendMessage(errorDialog, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                        await Task.Delay(100);
                    }
                }

                // Also look for dialog boxes by class name
                string[] dialogClasses = { "#32770", "Dialog" }; // Common dialog class names
                
                foreach (string className in dialogClasses)
                {
                    IntPtr dialog = FindWindow(className, null);
                    if (dialog != IntPtr.Zero)
                    {
                        // Check if it's a taskbar-related dialog by getting its text
                        var sb = new System.Text.StringBuilder(256);
                        GetWindowText(dialog, sb, sb.Capacity);
                        string windowText = sb.ToString().ToLower();
                        
                        if (windowText.Contains("taskbar") || windowText.Contains("can't have"))
                        {
                            LogDebug($"Closing taskbar error dialog: {windowText}");
                            SendMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                            await Task.Delay(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error closing taskbar error dialogs: {ex.Message}");
            }
        }

        private async Task ResetTaskbarSettings()
        {
            try
            {
                LogDebug("Resetting taskbar settings...");

                // Clear any conflicting registry entries
                await ClearTaskbarRegistryConflicts();
                
                // Reset taskbar state
                ResetTaskbarState();
                
                await Task.Delay(500);
                
                LogDebug("Taskbar settings reset completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error resetting taskbar settings: {ex.Message}");
            }
        }

        private async Task ClearTaskbarRegistryConflicts()
        {
            try
            {
                LogDebug("Clearing taskbar registry conflicts...");

                // Clear multi-monitor taskbar settings that might be conflicting
                string[] registryPaths = {
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3",
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\MMStuckRects3"
                };

                foreach (string path in registryPaths)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(path, true))
                        {
                            if (key != null)
                            {
                                // Get current settings
                                byte[] settings = (byte[])key.GetValue("Settings");
                                if (settings != null && settings.Length >= 9)
                                {
                                    // Clear any conflicting flags while preserving auto-hide setting
                                    bool currentAutoHide = (settings[8] & 0x08) != 0;
                                    settings[8] = (byte)(currentAutoHide ? 0x08 : 0x00);
                                    key.SetValue("Settings", settings, RegistryValueKind.Binary);
                                    LogDebug($"Cleared conflicts in {path}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Error clearing registry path {path}: {ex.Message}");
                    }
                }

                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                LogDebug($"Error clearing taskbar registry conflicts: {ex.Message}");
            }
        }

        private void ResetTaskbarState()
        {
            try
            {
                LogDebug("Resetting taskbar state...");

                // Find all taskbar windows
                IntPtr mainTaskbar = FindWindow("Shell_TrayWnd", null);
                if (mainTaskbar != IntPtr.Zero)
                {
                    // Reset main taskbar
                    APPBARDATA abd = new APPBARDATA();
                    abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                    abd.hWnd = mainTaskbar;
                    
                    // Get current state
                    uint currentState = SHAppBarMessage(ABM_GETSTATE, ref abd);
                    LogDebug($"Current taskbar state: {currentState}");
                    
                    // Reset to a clean state
                    abd.lParam = (int)ABS_ALWAYSONTOP; // Reset to always on top first
                    SHAppBarMessage(ABM_SETSTATE, ref abd);
                }

                // Look for secondary taskbars (Windows 10/11 multi-monitor)
                IntPtr secondaryTaskbar = FindWindow("Shell_SecondaryTrayWnd", null);
                while (secondaryTaskbar != IntPtr.Zero)
                {
                    LogDebug("Found secondary taskbar, resetting...");
                    
                    APPBARDATA abd = new APPBARDATA();
                    abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                    abd.hWnd = secondaryTaskbar;
                    abd.lParam = (int)ABS_ALWAYSONTOP;
                    SHAppBarMessage(ABM_SETSTATE, ref abd);
                    
                    // Look for next secondary taskbar
                    secondaryTaskbar = GetWindow(secondaryTaskbar, 2); // GW_HWNDNEXT
                    if (secondaryTaskbar != IntPtr.Zero)
                    {
                        var sb = new System.Text.StringBuilder(256);
                        GetClassName(secondaryTaskbar, sb, sb.Capacity);
                        if (sb.ToString() != "Shell_SecondaryTrayWnd")
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error resetting taskbar state: {ex.Message}");
            }
        }

        private void RefreshAllTaskbars()
        {
            try
            {
                LogDebug("Refreshing all taskbars...");

                // Refresh main taskbar
                IntPtr mainTaskbar = FindWindow("Shell_TrayWnd", null);
                if (mainTaskbar != IntPtr.Zero)
                {
                    SendMessage(mainTaskbar, 0x001A, IntPtr.Zero, IntPtr.Zero); // WM_SETTINGCHANGE
                    SendMessage(mainTaskbar, 0x0014, IntPtr.Zero, IntPtr.Zero); // WM_ERASEBKGND
                }

                // Refresh secondary taskbars
                IntPtr secondaryTaskbar = FindWindow("Shell_SecondaryTrayWnd", null);
                while (secondaryTaskbar != IntPtr.Zero)
                {
                    SendMessage(secondaryTaskbar, 0x001A, IntPtr.Zero, IntPtr.Zero); // WM_SETTINGCHANGE
                    SendMessage(secondaryTaskbar, 0x0014, IntPtr.Zero, IntPtr.Zero); // WM_ERASEBKGND
                    
                    secondaryTaskbar = GetWindow(secondaryTaskbar, 2); // GW_HWNDNEXT
                    if (secondaryTaskbar != IntPtr.Zero)
                    {
                        var sb = new System.Text.StringBuilder(256);
                        GetClassName(secondaryTaskbar, sb, sb.Capacity);
                        if (sb.ToString() != "Shell_SecondaryTrayWnd")
                            break;
                    }
                }

                // Force explorer refresh
                SendMessage(FindWindow("Progman", null), 0x001A, IntPtr.Zero, IntPtr.Zero);
                
                LogDebug("Taskbar refresh completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error refreshing taskbars: {ex.Message}");
            }
        }

        #endregion
    }
}
