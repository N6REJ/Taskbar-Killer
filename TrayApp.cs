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

        // HDMI input switching support
        private DateTime lastDisplayChangeTime = DateTime.MinValue;
        private bool isHdmiSwitchingDetected = false;
        private System.Windows.Forms.Timer hdmiStabilizationTimer;

        // Screen blanking detection
        private bool isScreenBlanked = false;
        private DateTime screenBlankStartTime = DateTime.MinValue;
        private System.Windows.Forms.Timer screenBlankRecoveryTimer;

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

                // Initialize HDMI switching support
                InitializeHdmiSwitchingSupport();

                // Initialize screen blanking detection
                InitializeScreenBlankingDetection();

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
                // More frequent dialog monitoring during HDMI switching
                if (isHdmiSwitchingDetected)
                {
                    await CloseTaskbarErrorDialogs();
                }
                else
                {
                    await CloseTaskbarErrorDialogs();
                }
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

            if (hdmiStabilizationTimer != null)
            {
                hdmiStabilizationTimer.Stop();
                hdmiStabilizationTimer.Dispose();
            }

            if (screenBlankRecoveryTimer != null)
            {
                screenBlankRecoveryTimer.Stop();
                screenBlankRecoveryTimer.Dispose();
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

                // Check if this is screen blanking recovery (highest priority)
                bool isScreenBlankRecovery = DetectScreenBlankingRecovery();
                
                // Check if this is HDMI switching
                bool isHdmiSwitch = DetectHdmiSwitching();
                lastDisplayChangeTime = DateTime.Now;

                if (isScreenBlankRecovery)
                {
                    LogDebug("Screen blanking recovery detected - using aggressive handling");
                    
                    // Start screen blank recovery timer
                    screenBlankRecoveryTimer.Stop();
                    screenBlankRecoveryTimer.Start();
                    
                    // Immediate and aggressive dialog cleanup for screen blanking recovery
                    await CloseTaskbarErrorDialogs();
                    await Task.Delay(200);
                    await CloseTaskbarErrorDialogs(); // Second immediate pass
                    
                    // Very short initial delay for screen blanking recovery
                    await Task.Delay(300);
                }
                else if (isHdmiSwitch)
                {
                    LogDebug("HDMI switching detected - using enhanced handling");
                    isHdmiSwitchingDetected = true;
                    
                    // Start stabilization timer for HDMI switching
                    hdmiStabilizationTimer.Stop();
                    hdmiStabilizationTimer.Start();
                    
                    // Immediate dialog cleanup for HDMI switching
                    await CloseTaskbarErrorDialogs();
                    
                    // Shorter initial delay for HDMI switching
                    await Task.Delay(500);
                }
                else
                {
                    // Standard display change handling
                    await Task.Delay(1000);
                }

                // Check for and resolve taskbar conflicts
                await ResolveTaskbarConflicts();

                // Restore auto-hide setting if it was enabled
                if (autoHideEnabled)
                {
                    LogDebug("Restoring auto-hide setting after display change");
                    
                    if (isScreenBlankRecovery)
                    {
                        // For screen blanking recovery, use the most aggressive restoration
                        await Task.Delay(100);
                        SetTaskbarAutoHide(true);
                        await Task.Delay(200);
                        SetTaskbarAutoHide(true);
                        await Task.Delay(200);
                        SetTaskbarAutoHide(true); // Triple-apply for screen blanking recovery
                    }
                    else if (isHdmiSwitch)
                    {
                        // For HDMI switching, use shorter delay and more aggressive restoration
                        await Task.Delay(200);
                        SetTaskbarAutoHide(true);
                        await Task.Delay(200);
                        SetTaskbarAutoHide(true); // Double-apply for HDMI switching
                    }
                    else
                    {
                        await Task.Delay(500);
                        SetTaskbarAutoHide(true);
                    }
                    
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
                    "taskbar", // Case-sensitive search
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
                        LogDebug($"Found error dialog by title: {title}");
                        SendMessage(errorDialog, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                        await Task.Delay(100);
                    }
                }

                // Enhanced dialog detection - look for all dialog windows
                EnumWindows(new EnumWindowsProc(EnumDialogWindows), IntPtr.Zero);

                // Additional search for dialogs with specific button text (OK, Cancel, etc.)
                await CloseDialogsWithButtons();

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
                            windowText.Contains("one auto-hide toolbar per side") ||
                            windowText.Contains("die of your screen") || // Typo in the original message
                            windowText.Contains("side of your screen") ||
                            (windowText.Contains("display") && windowText.Contains("taskbar")))
                        {
                            LogDebug($"Closing taskbar error dialog: {windowText}");
                            
                            // Try multiple close methods for stubborn dialogs
                            SendMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                            await Task.Delay(50);
                            
                            // Also try pressing ESC key
                            SendMessage(dialog, 0x0100, new IntPtr(0x1B), IntPtr.Zero); // WM_KEYDOWN ESC
                            await Task.Delay(50);
                            SendMessage(dialog, 0x0101, new IntPtr(0x1B), IntPtr.Zero); // WM_KEYUP ESC
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

        private async Task CloseDialogsWithButtons()
        {
            try
            {
                // Look for dialogs that might have OK/Cancel buttons and contain taskbar-related text
                EnumWindows(new EnumWindowsProc((hWnd, lParam) =>
                {
                    try
                    {
                        // Get window text
                        var windowText = new System.Text.StringBuilder(512);
                        GetWindowText(hWnd, windowText, windowText.Capacity);
                        string windowTextStr = windowText.ToString().ToLower();

                        // Check if this window contains the specific error message
                        if (windowTextStr.Contains("toolbar is already hidden") ||
                            windowTextStr.Contains("you can have only one auto-hide") ||
                            windowTextStr.Contains("die of your screen") ||
                            windowTextStr.Contains("side of your screen") ||
                            (windowTextStr.Contains("taskbar") && windowTextStr.Contains("already")))
                        {
                            LogDebug($"Found specific taskbar error dialog: {windowTextStr}");
                            
                            // Look for OK button in this dialog
                            IntPtr okButton = FindWindowEx(hWnd, IntPtr.Zero, "Button", "OK");
                            if (okButton != IntPtr.Zero)
                            {
                                LogDebug("Clicking OK button on taskbar error dialog");
                                SendMessage(okButton, 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK
                            }
                            else
                            {
                                // If no OK button, try to close the dialog directly
                                SendMessage(hWnd, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Error in button dialog enumeration: {ex.Message}");
                    }
                    return true;
                }), IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LogDebug($"Error closing dialogs with buttons: {ex.Message}");
            }
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

        private void InitializeHdmiSwitchingSupport()
        {
            try
            {
                LogDebug("Initializing HDMI switching support...");

                // Set up stabilization timer for HDMI input switching
                hdmiStabilizationTimer = new System.Windows.Forms.Timer();
                hdmiStabilizationTimer.Interval = 3000; // 3 second stabilization period
                hdmiStabilizationTimer.Tick += OnHdmiStabilizationTimer;

                LogDebug("HDMI switching support initialized successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"Error initializing HDMI switching support: {ex.Message}");
            }
        }

        private async void OnHdmiStabilizationTimer(object sender, EventArgs e)
        {
            try
            {
                LogDebug("HDMI stabilization timer triggered");
                hdmiStabilizationTimer.Stop();
                isHdmiSwitchingDetected = false;

                // Perform final cleanup and restoration after HDMI switching
                await PerformPostHdmiSwitchCleanup();
            }
            catch (Exception ex)
            {
                LogDebug($"Error in HDMI stabilization timer: {ex.Message}");
            }
        }

        private async Task PerformPostHdmiSwitchCleanup()
        {
            try
            {
                LogDebug("Performing post-HDMI switch cleanup...");

                // Close any lingering error dialogs
                await CloseTaskbarErrorDialogs();

                // Wait for display to fully stabilize
                await Task.Delay(1000);

                // Restore taskbar auto-hide if it was enabled
                if (autoHideEnabled)
                {
                    LogDebug("Restoring auto-hide after HDMI switch");
                    SetTaskbarAutoHide(true);
                    trayIcon.Icon = taskbarDownIcon;
                }

                // Force a taskbar refresh to ensure proper state
                RefreshTaskbar();

                LogDebug("Post-HDMI switch cleanup completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error in post-HDMI switch cleanup: {ex.Message}");
            }
        }

        private bool DetectHdmiSwitching()
        {
            try
            {
                DateTime now = DateTime.Now;
                TimeSpan timeSinceLastChange = now - lastDisplayChangeTime;

                // HDMI switching typically causes rapid display changes within a short time window
                // If we detect display changes within 5 seconds of each other, it's likely HDMI switching
                if (timeSinceLastChange.TotalSeconds < 5 && lastDisplayChangeTime != DateTime.MinValue)
                {
                    LogDebug($"HDMI switching detected - Time since last change: {timeSinceLastChange.TotalSeconds:F2} seconds");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"Error detecting HDMI switching: {ex.Message}");
                return false;
            }
        }

        private void InitializeScreenBlankingDetection()
        {
            try
            {
                LogDebug("Initializing screen blanking detection...");

                // Set up recovery timer for screen blanking scenarios
                screenBlankRecoveryTimer = new System.Windows.Forms.Timer();
                screenBlankRecoveryTimer.Interval = 5000; // 5 second recovery period
                screenBlankRecoveryTimer.Tick += OnScreenBlankRecoveryTimer;

                LogDebug("Screen blanking detection initialized successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"Error initializing screen blanking detection: {ex.Message}");
            }
        }

        private async void OnScreenBlankRecoveryTimer(object sender, EventArgs e)
        {
            try
            {
                LogDebug("Screen blank recovery timer triggered");
                screenBlankRecoveryTimer.Stop();
                isScreenBlanked = false;

                // Perform recovery actions after screen blanking
                await PerformScreenBlankRecovery();
            }
            catch (Exception ex)
            {
                LogDebug($"Error in screen blank recovery timer: {ex.Message}");
            }
        }

        private async Task PerformScreenBlankRecovery()
        {
            try
            {
                LogDebug("Performing screen blank recovery...");

                // Aggressive dialog cleanup for screen blanking recovery
                await CloseTaskbarErrorDialogs();
                await Task.Delay(500);
                await CloseTaskbarErrorDialogs(); // Second pass

                // Wait for display to stabilize after screen blanking
                await Task.Delay(1500);

                // Restore taskbar auto-hide if it was enabled
                if (autoHideEnabled)
                {
                    LogDebug("Restoring auto-hide after screen blank recovery");
                    
                    // Triple-apply for screen blanking scenarios (more aggressive than HDMI switching)
                    SetTaskbarAutoHide(true);
                    await Task.Delay(300);
                    SetTaskbarAutoHide(true);
                    await Task.Delay(300);
                    SetTaskbarAutoHide(true);
                    
                    trayIcon.Icon = taskbarDownIcon;
                }

                // Force multiple taskbar refreshes
                RefreshTaskbar();
                await Task.Delay(200);
                RefreshTaskbar();

                LogDebug("Screen blank recovery completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error in screen blank recovery: {ex.Message}");
            }
        }

        private bool DetectScreenBlankingRecovery()
        {
            try
            {
                DateTime now = DateTime.Now;
                
                // Check if we're coming back from a screen blank state
                if (isScreenBlanked)
                {
                    TimeSpan blankDuration = now - screenBlankStartTime;
                    LogDebug($"Screen blanking recovery detected - Blank duration: {blankDuration.TotalSeconds:F2} seconds");
                    return true;
                }

                // Detect potential screen blanking based on display configuration changes
                // Screen blanking often shows as temporary display configuration changes
                string currentConfig = GetDisplayConfiguration();
                if (currentConfig == "unknown" || currentConfig.Contains("0x0"))
                {
                    if (!isScreenBlanked)
                    {
                        LogDebug("Potential screen blanking detected - Display configuration shows unknown/zero resolution");
                        isScreenBlanked = true;
                        screenBlankStartTime = now;
                        return false; // Don't trigger recovery yet, wait for actual recovery
                    }
                }
                else if (isScreenBlanked)
                {
                    // We have a valid configuration and were previously blanked - this is recovery
                    LogDebug("Screen blanking recovery detected - Valid display configuration restored");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"Error detecting screen blanking recovery: {ex.Message}");
                return false;
            }
        }
    }
}
