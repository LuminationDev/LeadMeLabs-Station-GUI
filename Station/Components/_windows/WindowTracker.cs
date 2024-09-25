using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Station.Components._models;

namespace Station.Components._windows;

/// <summary>
/// Hooks into window events to dynamically update the list of visible windows.
/// Calls WindowManager to get and manipulate window properties.
/// Maintains the list (visibleWindows) of currently visible windows.
/// </summary>
public class WindowTracker
{
    // Constants for event hooks (In order of event number)
    private const int EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const int EVENT_SYSTEM_MINIMISESTART = 0x0016;
    private const int EVENT_SYSTEM_MINIMISEEND = 0x0017;
    private const int EVENT_OBJECT_DESTROY = 0x8001;
    private const int EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    
    // List to store the visible windows
    private static readonly HashSet<WindowInformation> VisibleWindows = new();

    // Primary screen index of Screens.AllScreens - used for coordinating Window movement and restrictions
    public static int PrimaryScreenIndex;
    
    // Screen used for interaction with the pod - will always be the smallest size?
    public static int TouchScreenIndex;
    
    // Delegate for the callback function
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // Hook handle
    private IntPtr _hookHandle;
    // Store the delegate to prevent garbage collection
    private WinEventDelegate? _callback;

    /// <summary>
    /// Starts tracking window events by setting up a Windows event hook.
    /// </summary>
    /// <remarks>
    /// This method hooks into the system events for foreground window changes and window destruction.
    /// It also refreshes the list of currently visible windows after setting up the hook.
    /// </remarks>
    public void StartTracking()
    {
        GetScreenIndexes();
        RefreshVisibleWindows();
        _callback = WinEventProc;
        _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _callback, 0, 0, 0);
    }

    /// <summary>
    /// Find the index of the primary screen and the touch screen.
    /// </summary>
    private void GetScreenIndexes()
    {
        // No screens available
        if (Screen.AllScreens.Length == 0)
        {
            return;
        }
        
        PrimaryScreenIndex = Array.FindIndex(Screen.AllScreens, screen => screen.Bounds == Screen.PrimaryScreen.Bounds);
        TouchScreenIndex = Screen.AllScreens
            .Select((screen, index) => new { Screen = screen, Index = index })
            .OrderBy(s => s.Screen.Bounds.Width * s.Screen.Bounds.Height)
            .First().Index;
    }

    /// <summary>
    /// Stops tracking window events by removing the existing Windows event hook.
    /// </summary>
    /// <remarks>
    /// This method unhooks the event listener, clears the hook handle, and optionally clears the delegate reference.
    /// After calling this method, no further window events will be tracked until <see cref="StartTracking"/> is called again.
    /// </remarks>
    public void StopTracking()
    {
        UnhookWinEvent(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _callback = null;
        VisibleWindows.Clear();
    }
    
    /// <summary>
    /// Handles Windows events and processes them based on their type.
    /// </summary>
    /// <param name="hWinEventHook">The handle to the event hook.</param>
    /// <param name="eventType">The type of the event that occurred.</param>
    /// <param name="hWnd">The handle to the window associated with the event.</param>
    /// <param name="idObject">The object identifier.</param>
    /// <param name="idChild">The child identifier.</param>
    /// <param name="dwEventThread">The thread identifier where the event occurred.</param>
    /// <param name="dwmsEventTime">The time the event occurred.</param>
    /// <remarks>
    /// This method retrieves the window title and size, and processes the event to handle window openings, 
    /// minimise events, and closures accordingly. If the window title is empty or the window has no size, 
    /// the method exits early.
    /// </remarks>
    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        string? title = WindowManager.GetWindowTitle(hWnd);
        
        // Early return if the window title is empty or the window has no size
        if (string.IsNullOrWhiteSpace(title?.Trim()) || !WindowManager.HasWindowSize(hWnd)) return;
        
        // Create window information object
        var windowInfo = new WindowInformation
        {
            Handle = hWnd,
            Title = title,
            Rect = WindowManager.GetWindowRect(hWnd),
            Minimised = WindowManager.IsIconic(hWnd)
        };

        // Process the event based on its type
        switch (eventType)
        {
            case EVENT_SYSTEM_FOREGROUND:
                HandleWindowOpened(windowInfo);
                break;
            
            case EVENT_OBJECT_LOCATIONCHANGE:
                HandleWindowMoved(hWnd);
                break;

            case EVENT_SYSTEM_MINIMISESTART:
                UpdateWindowMinimisedState(hWnd, true);
                break;

            case EVENT_SYSTEM_MINIMISEEND:
                UpdateWindowMinimisedState(hWnd, false);
                break;

            case EVENT_OBJECT_DESTROY:
                HandleWindowClosed(windowInfo);
                break;
        }
    }

    /// <summary>
    /// Handles the event when a window is opened and adds it to the collection of visible windows.
    /// </summary>
    /// <param name="windowInfo">An object containing information about the opened window.</param>
    /// <remarks>
    /// If the window is successfully added to the collection, a message is logged indicating that the 
    /// window has been opened and added.
    /// </remarks>
    private void HandleWindowOpened(WindowInformation windowInfo)
    {
        if (!VisibleWindows.Add(windowInfo)) return;
        
        int screenIndex = GetScreenIndexForWindow(windowInfo.Rect);
        windowInfo.Monitor = screenIndex;
        
        MoveWindowOffRestrictedMonitor(windowInfo);
    }
    
    /// <summary>
    /// Updates an existing VisibleWindows entry after a window has been moved. Updates the position and monitor. If
    /// the Window has been moved onto restricted space it is automatically moved away.
    /// </summary>
    /// <param name="hWnd">The handle to the window associated with the event.</param>
    private void HandleWindowMoved(IntPtr hWnd)
    {
        var existingWindow = VisibleWindows.FirstOrDefault(w => w.Handle == hWnd);
        if (existingWindow == null) return;
    
        var newRect = WindowManager.GetWindowRect(hWnd);
    
        // Check if the position has actually changed
        if (newRect.Left == existingWindow.Rect.Left && newRect.Top == existingWindow.Rect.Top) return;
        
        // Update the stored monitor and position
        int screenIndex = GetScreenIndexForWindow(existingWindow.Rect);
        existingWindow.Monitor = screenIndex;
        existingWindow.Rect = newRect; 
        
        MoveWindowOffRestrictedMonitor(existingWindow);
    }

    /// <summary>
    /// Updates the minimised state of a specified window.
    /// </summary>
    /// <param name="hWnd">The handle to the window whose minimised state is to be updated.</param>
    /// <param name="isMinimised">Indicates whether the window is minimised (<c>true</c>) or restored (<c>false</c>).</param>
    /// <remarks>
    /// If the window is found in the collection, its minimised state is updated, and a message is logged 
    /// indicating whether the window was minimised or restored.
    /// </remarks>
    private void UpdateWindowMinimisedState(IntPtr hWnd, bool isMinimised)
    {
        var existingWindow = VisibleWindows.FirstOrDefault(w => w.Handle == hWnd);
        if (existingWindow == null) return;
        
        existingWindow.Minimised = isMinimised;
    }
    
    /// <summary>
    /// Handles the event when a window is closed and removes it from the collection of visible windows.
    /// </summary>
    /// <param name="windowInfo">An object containing information about the closed window.</param>
    /// <remarks>
    /// If the window is successfully removed from the collection, a message is logged indicating that the 
    /// window has been closed and removed.
    /// </remarks>
    private void HandleWindowClosed(WindowInformation windowInfo)
    {
        VisibleWindows.Remove(windowInfo);
    }

    /// <summary>
    /// Detects if an authorised windows moves or is opened on the restricted monitor, proceeds to move it to the primary
    /// monitor instead.
    /// </summary>
    private void MoveWindowOffRestrictedMonitor(WindowInformation windowInfo)
    {
        if (TouchScreenIndex != windowInfo.Monitor || windowInfo.Title == "The Pod") return;
        
        Screen s = Screen.AllScreens[PrimaryScreenIndex];
        Rectangle r  = s.WorkingArea;
        WindowManager.MoveWindow(windowInfo.Handle, r.Left, r.Top, windowInfo.Rect.Right - windowInfo.Rect.Left, windowInfo.Rect.Bottom - windowInfo.Rect.Top);
    }
    
    /// <summary>
    /// Refreshes the list of currently visible windows by enumerating all visible windows and updating the internal collection.
    /// </summary>
    /// <remarks>
    /// This method clears the existing collection of visible windows and repopulates it with the currently visible windows.
    /// Each window's information, including its handle, title, dimensions, and minimised state, is gathered and added to the collection.
    /// </remarks>
    private static void RefreshVisibleWindows()
    {
        VisibleWindows.Clear();
        WindowManager.EnumVisibleWindows((hWnd, title, rect) =>
        {
            var windowInfo = new WindowInformation
            {
                Handle = hWnd,
                Title = title,
                Rect = rect,
                Minimised = WindowManager.IsIconic(hWnd)
            };

            VisibleWindows.Add(windowInfo);
        });
    }

    /// <summary>
    /// Determines which monitor contains the largest portion of the specified window, 
    /// based on the window's rectangle and the bounds of all available monitors.
    /// </summary>
    /// <param name="windowRect">The rectangle (position and size) of the window to evaluate.</param>
    /// <returns>
    /// The index of the monitor containing the largest portion of the window, 
    /// or -1 if the window does not overlap with any monitor.
    /// </returns>
    /// <remarks>
    /// This method calculates the intersection area between the window's rectangle 
    /// and each monitor's bounds. The monitor with the largest intersection area is returned. 
    /// If no intersection is found, -1 is returned.
    /// </remarks>
    private static int GetScreenIndexForWindow(WindowManager.RECT windowRect)
    {
        int largestArea = 0;
        int monitorIndex = -1;

        // Iterate over all screens to find the monitor containing the largest portion of the window
        for (int i = 0; i < Screen.AllScreens.Length; i++)
        {
            // Get the screen's bounds (working area)
            Rectangle screenRect = Screen.AllScreens[i].Bounds;

            // Calculate the intersection area between the window and the screen
            int intersectionWidth = Math.Min(windowRect.Right, screenRect.Right) - Math.Max(windowRect.Left, screenRect.Left);
            int intersectionHeight = Math.Min(windowRect.Bottom, screenRect.Bottom) - Math.Max(windowRect.Top, screenRect.Top);

            // If there's a valid intersection (width and height both greater than 0)
            if (intersectionWidth <= 0 || intersectionHeight <= 0) continue;
            
            // Calculate the area of the intersection
            int intersectionArea = intersectionWidth * intersectionHeight;

            // Check if this intersection is larger than the previous largest area
            if (intersectionArea <= largestArea) continue;
            
            largestArea = intersectionArea;
            monitorIndex = i; // Store the index of the monitor with the largest intersection
        }

        // Return the index of the monitor that contains the largest portion of the window, or -1 if none
        return monitorIndex;
    }
    
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint dwProcessId, uint dwThreadId, uint dwmsEventTime);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
