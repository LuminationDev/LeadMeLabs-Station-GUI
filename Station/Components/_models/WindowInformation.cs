using System;
using Station.Components._windows;

namespace Station.Components._models;

public class WindowInformation
{
    public IntPtr Handle { get; init; }
    public string? Title { get; init; }
    public WindowManager.RECT Rect { get; set; }
    public bool Minimised { get; set; }
    public int Monitor { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is WindowInformation other && Handle == other.Handle;
    }

    public override int GetHashCode()
    {
        return Handle.GetHashCode();
    }
    
    public override string ToString()
    {
        return $"Title: {Title}, Monitor: {Monitor}, Handle: {Handle}, Minimised: {Minimised}, Position: ({Rect.Left}, {Rect.Top}), Size: ({Rect.Right - Rect.Left}, {Rect.Bottom - Rect.Top})";
    }
}
