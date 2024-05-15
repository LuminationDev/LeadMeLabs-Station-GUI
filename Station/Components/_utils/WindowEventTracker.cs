using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Station.Components._notification;
using Station.Components._utils;
using Station.MVC.Controller;

public class WindowEventTracker
{
    private uint m_processId, m_threadId;

    private IntPtr m_target;

    // Needed to prevent the GC from sweeping up our callback
    private static WinEventDelegate m_winEventDelegateSteam;
    private static WinEventDelegate m_winEventDelegateMouse;
    private static IntPtr m_hookSteam;
    private static IntPtr m_hookMouse;

    private static DateTime nextReportTime = DateTime.Now;
    
    private static DateTime lastInteraction = DateTime.Now;

    private bool _minimisingEnabled = true;

    /**
     * Keeping this function here as it was quite tricky to write
     */
    private static bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
    {
        if (IsWindowVisible(hWnd))
        {
            // Get the window title
            const int nChars = 256;
            System.Text.StringBuilder title = new System.Text.StringBuilder(nChars);
            if (GetWindowText(hWnd, title, nChars) > 0)
            {
                Console.WriteLine("Window Handle: " + hWnd);
                Console.WriteLine("Window Title: " + title);
                Console.WriteLine();
            }
        }
    
        // Continue enumerating
        return true;
    }

    ~WindowEventTracker()
    {
        Unsubscribe();
    }
    
    public WindowEventTracker()
    {
        // EnumWindows(EnumWindowsCallback, IntPtr.Zero); // keep this here as a reference if needed
        
        // ReSharper disable once InvalidXmlDocComment
        /**
         * IMPORTANT: This must be run on the main thread
         * If not, it will not run
         *
         * We initializing here, and then altering the target, process id and thread id when needed
         */
        m_winEventDelegateSteam = WhenWindowMoveStartsOrEnds;
        m_winEventDelegateMouse = MouseClick;
        m_hookSteam = SetWinEventHook(0, 100, m_target, m_winEventDelegateSteam, m_processId, m_threadId, 0);
        m_hookMouse = SetWinEventHook(8, 8, m_target, m_winEventDelegateMouse, 0, 0, 0);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // Callback function for EnumWindows
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private void ThrowOnWin32Error(string message)
    {
        int err = Marshal.GetLastWin32Error();
        if (err != 0)
            throw new Win32Exception(err, message);
    }

    public void Subscribe(string windowName, string className)
    {
        if (windowName == null && className == null) throw new ArgumentException("Either windowName or className must have a value");

        m_target = FindWindow(className, windowName);
        ThrowOnWin32Error("Failed to get target window");

        m_threadId = GetWindowThreadProcessId(m_target, out m_processId);
        ThrowOnWin32Error("Failed to get process id");
        
        WindowManager.MinimizeProcess(Process.GetProcessById(Convert.ToInt32(m_processId)));
    }

    public void Unsubscribe()
    {
        UnhookWinEvent(m_hookSteam);
        UnhookWinEvent(m_hookMouse);
    }

    private void WhenWindowMoveStartsOrEnds(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd != m_target) // We only want events from our target window, not other windows owned by the thread.
            return;

        if (eventType == 10) // Starts
        {
            Console.WriteLine("moving");
        }
        else if (eventType == 11)
        {
            Console.WriteLine("stopped");
        }
        else if (eventType == 22) // Starts
        {
            //Console.WriteLine("minimize");
        }
        else if (eventType == 23) // Starts
        {
            // Console.WriteLine("maximise");
            if (_minimisingEnabled)
            {
                if ((DateTime.Now < lastInteraction.AddSeconds(1) ||  DateTime.Now > lastInteraction.AddSeconds(10)) && InternalDebugger.GetMinimisePrograms())
                {
                    WindowManager.MinimizeProcess(Process.GetProcessById(Convert.ToInt32(m_processId)));
                    lastInteraction = DateTime.Now;
                }
            }
        }
        else
        {
            Logger.WriteLog(eventType.ToString(), MockConsole.LogLevel.Debug);
        }
    }
    
    private void MouseClick(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (nextReportTime > DateTime.Now)
        {
            nextReportTime = DateTime.Now.AddMinutes(10);
            MessageController.SendResponse("NUC", "Analytics", "KeyboardInteraction");
        }
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    public void SetMinimisingEnabled(bool value)
    {
        this._minimisingEnabled = value;
    }
}