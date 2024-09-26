using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LeadMeLabsLibrary;
using Station.Components._utils;

namespace Station.Components._windows;

/// <summary>
/// Manages low-level interactions (enumerating windows, moving windows, getting window positions, window visibility)
/// Focuses on window operations without tracking logic
/// </summary>
public static class WindowManager
{
    #region Window Visibility
    private const int SW_HIDE = 0;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int SW_SHOWMINNOACTIVE = 7;
    
    public static void HideProcess(Process process)
    {
        if (process != null)
        {
            ShowWindow(process.MainWindowHandle, SW_HIDE);
        } else
        {
            Logger.WriteLog("A process was null when trying to hide", Enums.LogLevel.Normal);
        }
    }

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

    public static void MinimizeWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, SW_SHOWMINIMIZED);
    }

    public static void MinimizeProcessNoActivate(Process process)
    {
        if (process != null)
        {
            ShowWindow(process.MainWindowHandle, SW_SHOWMINNOACTIVE);
        } else
        {
            Logger.WriteLog("A process was null when trying to minimise no activate", Enums.LogLevel.Normal);
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
    #endregion

    #region Window Operations (Enum, Move, Get Rect)
    // Flags for setting window position
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private static readonly IntPtr HWND_TOP = new(0);

    /// <summary>
    /// Enumerates all visible windows on the screen and executes a specified action for each visible window with a
    /// valid title and size.
    /// </summary>
    /// <param name="action">A callback action that receives the handle, title, and bounding rectangle of each visible window.</param>
    /// <remarks>
    /// The method skips any windows that are not visible or do not have a valid title or size.
    /// </remarks>
    public static void EnumVisibleWindows(Action<IntPtr, string, RECT> action)
    {
        EnumWindows((hWnd, lParam) =>
        {
            // Check if the window is visible
            if (!IsWindowVisible(hWnd)) return true; // Skip invisible windows

            // Get the window title
            string? title = GetWindowTitle(hWnd);

            // Only call the callback if the title is valid
            if (!string.IsNullOrWhiteSpace(title) && HasWindowSize(hWnd))
            {
                GetWindowRect(hWnd, out RECT rect);
                action(hWnd, title, rect);
            }

            return true; // Continue enumerating
        }, IntPtr.Zero);
    }

    /// <summary>
    /// Moves the specified window to a new position on the screen.
    /// </summary>
    /// <param name="hWnd">The handle to the window to be moved.</param>
    /// <param name="x">The new x-coordinate of the window's upper-left corner.</param>
    /// <param name="y">The new y-coordinate of the window's upper-left corner.</param>
    /// <param name="width">The width of the window being moved.</param>
    /// <param name="height">The height of the window being moved</param>
    public static void MoveWindow(IntPtr hWnd, int x, int y, int width, int height)
    {
        // Move the window to the new position without changing its size or Z-order
        SetWindowPos(hWnd, HWND_TOP, x, y, width, height, SWP_NOSIZE | SWP_NOZORDER);
    }
    
    /// <summary>
    /// Retrieves the bounding rectangle of the specified window.
    /// </summary>
    /// <param name="hWnd">The handle to the window whose rectangle is to be retrieved.</param>
    /// <returns>A <see cref="RECT"/> structure representing the dimensions of the window.</returns>
    public static RECT GetWindowRect(IntPtr hWnd)
    {
        GetWindowRect(hWnd, out RECT rect);
        return rect;
    }
    
    /// <summary>
    /// Determines whether the specified window has a non-zero size.
    /// </summary>
    /// <param name="hWnd">The handle to the window to check.</param>
    /// <returns><c>true</c> if the window has a width and height greater than zero; otherwise, <c>false</c>.</returns>
    public static bool HasWindowSize(IntPtr hWnd)
    {
        // Get the window size using GetWindowRect
        if (!GetWindowRect(hWnd, out RECT rect)) return false;
        
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        // Check if width and height are greater than 0
        return width > 0 && height > 0;
    }
    
    /// <summary>
    /// Retrieves the title of the specified window.
    /// </summary>
    /// <param name="hWnd">The handle to the window whose title is to be retrieved.</param>
    /// <returns>The title of the window, or <c>null</c> if the window has no title or an error occurs.</returns>
    public static string? GetWindowTitle(IntPtr hWnd)
    {
        const int nChars = 256;
        StringBuilder buff = new StringBuilder(nChars);
        return GetWindowText(hWnd, buff, nChars) > 0 ? buff.ToString() : null;
    }
    
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    // Structure to hold window coordinates
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    #endregion
}
