using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

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
    }
}
