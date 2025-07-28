using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
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

        // Enhanced dialog monitoring
        private System.Windows.Forms.Timer dialogMonitorTimer;

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
                trayMenu.Items.Add("Close Taskbar Dialogs", null, ManualCloseDialogs);
                trayMenu.Items.Add("Add to Startup", null, AddToStartup);
                trayMenu.Items.Add("Remove from Startup", null, RemoveFromStartup);
                trayMenu.Items.Add("Exit", null, OnExit);

                LogDebug("Context menu created");

                trayIcon = new NotifyIcon();
                trayIcon.Text = "Taskbar Killer Enhanced";
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

                // Initialize enhanced dialog monitoring
                InitializeDialogMonitoring();

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

        private void InitializeDialogMonitoring()
        {
            try
            {
                LogDebug("Initializing enhanced dialog monitoring...");

                // Set up timer for continuous dialog monitoring
                dialogMonitorTimer = new System.Windows.Forms.Timer();
                dialogMonitorTimer.Interval = 1000; // Check every 1 second
                dialogMonitorTimer.Tick += OnDialogMonitorTimer;
                dialogMonitorTimer.Start();

                LogDebug("Enhanced dialog monitoring initialized successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"Error initializing dialog monitoring: {ex.Message}");
            }
        }

        private async void OnDialogMonitorTimer(object sender, EventArgs e)
        {
            try
            {
                await CloseTaskbarErrorDialogs();
            }
            catch (Exception ex)
            {
                LogDebug($"Error in dialog monitor timer: {ex.Message}");
            }
        }

        private void LogDebug(string message)
        {
            try
            {
                string logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "debug_enhanced.log");
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

            // Cleanup timers
            if (displayChangeTimer != null)
            {
                displayChangeTimer.Stop();
                displayChangeTimer.Dispose();
            }

            if (dialogMonitorTimer != null)
            {
                dialogMonitorTimer.Stop();
                dialogMonitorTimer.Dispose();
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

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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
                // First try to load from the icons subdirectory (for single-file publishing)
                string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string iconsDir = System.IO.Path.Combine(exeDir, "icons");
                string icoPath = System.IO.Path.Combine(iconsDir, "taskbar.ico");
                
                LogDebug($"Trying to load icon from icons directory: {icoPath}");
                LogDebug($"Icons directory exists: {System.IO.Directory.Exists(iconsDir)}");
                LogDebug($"Icon file exists: {System.IO.File.Exists(icoPath)}");
                
                if (System.IO.File.Exists(icoPath))
                {
                    Icon icon = new Icon(icoPath);
                    LogDebug($"Icon loaded successfully from icons directory: {icon != null}");
                    return icon;
                }
                
                // Fallback: try to load from the executable directory
                string fallbackIcoPath = System.IO.Path.Combine(exeDir, "taskbar.ico");
                LogDebug($"Trying fallback icon path: {fallbackIcoPath}");
                LogDebug($"Fallback file exists: {System.IO.File.Exists(fallbackIcoPath)}");
                
                if (System.IO.File.Exists(fallbackIcoPath))
                {
                    Icon icon = new Icon(fallbackIcoPath);
                    LogDebug($"Icon loaded successfully from fallback path: {icon != null}");
                    return icon;
                }
                
                LogDebug("Using SystemIcons.Application as fallback");
                return SystemIcons.Application; // Use a visible system icon
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading icon: {ex.Message}");
                return SystemIcons.Error; // Use error icon so we can see SOMETHING
            }
        }

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

                // Look for common error dialog patterns - including your specific error
                string[] errorTitles = {
                    "Taskbar",
                    "Windows Shell",
                    "Explorer",
                    "can't have 2 taskbars",
                    "taskbar error",
                    "Taskbar Settings",
                    "Display Settings",
                    "Multiple Taskbars",
                    "Taskbar Conflict",
                    "A toolbar is already hidden",
                    "auto-hide toolbar"
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

                // Enhanced dialog detection - look for all dialog windows
                EnumWindows(new EnumWindowsProc(EnumDialogWindows), IntPtr.Zero);

                // Also look for dialog boxes by class name
                string[] dialogClasses = { "#32770", "Dialog", "MessageBox" }; // Common dialog class names

                foreach (string className in dialogClasses)
                {
                    IntPtr dialog = FindWindow(className, null);
                    while (dialog != IntPtr.Zero)
                    {
                        // Check if it's a taskbar-related dialog by getting its text
                        var sb = new System.Text.StringBuilder(256);
                        GetWindowText(dialog, sb, sb.Capacity);
                        string windowText = sb.ToString().ToLower();

                        if (windowText.Contains("taskbar") || 
                            windowText.Contains("can't have") ||
                            windowText.Contains("already active") ||
                            windowText.Contains("already hidden") ||
                            windowText.Contains("auto-hide toolbar") ||
                            windowText.Contains("toolbar is already") ||
                            windowText.Contains("multiple taskbar") ||
                            windowText.Contains("you can have only one") ||
                            (windowText.Contains("display") && windowText.Contains("taskbar")))
                        {
                            LogDebug($"Closing taskbar error dialog: {windowText}");
                            SendMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                            await Task.Delay(100);
                        }

                        // Find next dialog of same class
                        dialog = FindWindowEx(IntPtr.Zero, dialog, className, null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error closing taskbar error dialogs: {ex.Message}");
            }
        }

        private bool EnumDialogWindows(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                // Get window class name
                var className = new System.Text.StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                string classNameStr = className.ToString();

                // Check if it's a dialog window
                if (classNameStr == "#32770" || classNameStr == "Dialog")
                {
                    // Get window text
                    var windowText = new System.Text.StringBuilder(256);
                    GetWindowText(hWnd, windowText, windowText.Capacity);
                    string windowTextStr = windowText.ToString().ToLower();

                    // Check if it's a taskbar-related dialog - including the specific error you mentioned
                    if (windowTextStr.Contains("taskbar") || 
                        windowTextStr.Contains("can't have") ||
                        windowTextStr.Contains("already active") ||
                        windowTextStr.Contains("already hidden") ||
                        windowTextStr.Contains("auto-hide toolbar") ||
                        windowTextStr.Contains("toolbar is already") ||
                        windowTextStr.Contains("you can have only one") ||
                        windowTextStr.Contains("multiple taskbar") ||
                        (windowTextStr.Contains("display") && windowTextStr.Contains("taskbar")))
                    {
                        LogDebug($"EnumWindows found taskbar dialog: {windowTextStr}");
                        SendMessage(hWnd, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in EnumDialogWindows: {ex.Message}");
            }

            return true; // Continue enumeration
        }

        private async void ManualCloseDialogs(object sender, EventArgs e)
        {
            try
            {
                LogDebug("Manual dialog close requested");
                await CloseTaskbarErrorDialogs();
                MessageBox.Show("Checked for and closed any taskbar error dialogs.", "Dialog Cleanup", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogDebug($"Error in manual dialog close: {ex.Message}");
                MessageBox.Show($"Error closing dialogs: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
