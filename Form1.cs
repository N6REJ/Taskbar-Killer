using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskbarAutoHideOnResume
{
    public partial class Form1 : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool autoHideEnabled;
        private Icon taskbarUpIcon;
        private Icon taskbarDownIcon;
        private string taskbarUpPath = "icons/taskbar-up.png";
        private string taskbarDownPath = "icons/taskbar-down.png";

        public Form1()
        {
            try
            {
                LogDebug("Application starting...");
                
                // No designer, so no InitializeComponent();
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Visible = false;

                LogDebug("Form initialized, loading icons...");

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
                
                LogDebug("Application initialization complete");
            }
            catch (Exception ex)
            {
                LogDebug($"ERROR: {ex.Message}\nStack trace: {ex.StackTrace}");
                MessageBox.Show($"Error initializing application: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
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
            this.Close();
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Prevent the form from closing unless we're exiting
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            
            trayIcon.Visible = false;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            base.OnFormClosing(e);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Keep the form hidden
            base.SetVisibleCore(false);
        }

        private Icon LoadIconFromPng(string path)
        {
            try
            {
                // Get the directory where the executable is located
                string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string fullPath = System.IO.Path.Combine(exeDir, path);

                if (System.IO.File.Exists(fullPath))
                {
                    using (Bitmap bmp = new Bitmap(fullPath))
                    {
                        // Resize to standard icon size for better compatibility
                        using (Bitmap resized = new Bitmap(bmp, new Size(16, 16)))
                        {
                            // Convert bitmap to icon
                            IntPtr hIcon = resized.GetHicon();
                            Icon icon = Icon.FromHandle(hIcon);
                            // Note: Icon.FromHandle creates a copy, so we need to destroy the original handle
                            DestroyIcon(hIcon);
                            return icon;
                        }
                    }
                }
                else
                {
                    // Try to load the .ico file as fallback
                    string icoPath = System.IO.Path.Combine(exeDir, "taskbar.ico");
                    if (System.IO.File.Exists(icoPath))
                    {
                        return new Icon(icoPath);
                    }
                    // Final fallback to system icon
                    return SystemIcons.Application;
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging (optional)
                System.Diagnostics.Debug.WriteLine($"Error loading icon from {path}: {ex.Message}");

                // Try to load the .ico file as fallback
                try
                {
                    string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                    string icoPath = System.IO.Path.Combine(exeDir, "taskbar.ico");
                    if (System.IO.File.Exists(icoPath))
                    {
                        return new Icon(icoPath);
                    }
                }
                catch (Exception)
                {
                    // Ignore fallback errors
                }

                // Final fallback to system icon
                return SystemIcons.Application;
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
