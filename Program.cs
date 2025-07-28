using System;
using System.Windows.Forms;

namespace TaskbarAutoHideOnResume
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Use TrayApp ApplicationContext to keep the app running
            Application.Run(new TrayApp());
        }
    }
}
