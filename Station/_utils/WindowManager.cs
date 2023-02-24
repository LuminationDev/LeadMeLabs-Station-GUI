using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Station
{
    public class WindowManager
    {
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;

        public static void MinimizeProcess(Process process)
        {
            ShowWindow(process.MainWindowHandle, SW_SHOWMINIMIZED);
        }

        public static void MaximizeProcess(Process process)
        {
            ShowWindow(process.MainWindowHandle, SW_SHOWMAXIMIZED);
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
