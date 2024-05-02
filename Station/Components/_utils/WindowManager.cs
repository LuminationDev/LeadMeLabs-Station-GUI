using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LeadMeLabsLibrary;

namespace Station.Components._utils;

public static class WindowManager
{
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;

    public static void MinimizeProcess(Process process)
    {
        if (process != null)
        {
            ShowWindow(process.MainWindowHandle, SW_SHOWMINIMIZED);
        } else
        {
            Logger.WriteLog("A process was null when trying to minimise", Enums.LogLevel.Normal);
        }
    }

    public static void MaximizeProcess(Process process)
    {
        if (process != null)
        {
            ShowWindow(process.MainWindowHandle, SW_SHOWMAXIMIZED);
        }
        else
        {
            Logger.WriteLog("A process was null when trying to maximise", Enums.LogLevel.Normal);
        }
    }

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
