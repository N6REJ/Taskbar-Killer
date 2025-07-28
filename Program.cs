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
            
            // Use TrayAppEnhanced ApplicationContext to keep the app running
            Application.Run(new TrayAppEnhanced());
        }
    }
}
