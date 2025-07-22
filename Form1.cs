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
        private string taskbarUpPath = "icon/taskbar-up.png";
        private string taskbarDownPath = "icon/taskbar-down.png";

        public Form1()
        {
            // No designer, so no InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            // Load PNG icons directly
            taskbarUpIcon = LoadIconFromPng(taskbarUpPath);
            taskbarDownIcon = LoadIconFromPng(taskbarDownPath);

            // Read the current auto-hide state from registry
            autoHideEnabled = GetTaskbarAutoHide();

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Toggle Auto-Hide", null, ToggleAutoHide);
            trayMenu.Items.Add("Add to Startup", null, AddToStartup);
            trayMenu.Items.Add("Remove from Startup", null, RemoveFromStartup);
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Taskbar Auto-Hide Trigger";
            trayIcon.Icon = autoHideEnabled ? taskbarDownIcon : taskbarUpIcon;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += ToggleAutoHide;
            trayIcon.MouseClick += TrayIcon_MouseClick;

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
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
            trayIcon.Visible = false;
            Application.Exit();
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
            var key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StuckRects3", true);
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
                    key.Close();
                    ApplyTaskbarAutoHide(enable);
                    return true;
                }
                key.Close();
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
            var key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StuckRects3", false);
            if (key != null)
            {
                byte[] value = (byte[])key.GetValue("Settings");
                key.Close();
                if (value != null && value.Length >= 9)
                {
                    return (value[8] & 0x08) != 0;
                }
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
            trayIcon.Visible = false;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            base.OnFormClosing(e);
        }

        private Icon LoadIconFromPng(string path)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(path))
                {
                    // Convert bitmap to icon
                    IntPtr hIcon = bmp.GetHicon();
                    Icon icon = Icon.FromHandle(hIcon);
                    return icon;
                }
            }
            catch (Exception)
            {
                // Fallback to system icon if error
                return SystemIcons.Application;
            }
        }
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
