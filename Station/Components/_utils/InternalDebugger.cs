using System;
using Station.Components._notification;

namespace Station.Components._utils;

public static class InternalDebugger
{
    /// <summary>
    /// Used to control if the VR programs should minimise or not
    /// </summary>
    public static bool minimiseVrPrograms = true;
    public static bool GetMinimisePrograms()
    {
        if (!minimiseVrPrograms)
        {
            MockConsole.WriteLine("WARNING: Will not minimise vr programs, Auto Minimise set to 'No' in Debug Panel.");
        }
        
        return minimiseVrPrograms;
    }
    
    /// <summary>
    /// Used to control if the VR programs should minimise or not
    /// </summary>
    public static bool autoScroll = true;
    public static bool GetAutoScroll()
    {
        return autoScroll;
    }
    
    /// <summary>
    /// Used to control if the VR programs should minimise or not
    /// </summary>
    public static bool autoStartVrPrograms = true;
    public static bool GetAutoStart()
    {
        if (!autoStartVrPrograms)
        {
            MockConsole.WriteLine("WARNING: Will not launch SteamVR, Auto Start VR Software set to 'No' in Debug Panel.");
        }
        
        return autoStartVrPrograms;
    }
    
    /// <summary>
    /// Used to control if the VR programs should minimise or not
    /// </summary>
    public static bool headsetRequired = true;
    public static bool GetHeadsetRequired()
    {
        if (!headsetRequired)
        {
            MockConsole.WriteLine("WARNING: Experiences will launch without a headset connected, Headset required set to 'No' in Debug Panel.");
        }
        
        return headsetRequired;
    }
    
    /// <summary>
    /// Used to control the Station should go into Idle mode after 60 minutes of no usage
    /// </summary>
    public static bool? idleModeActive = null;
    public static void SetIdleModeActive(bool active, bool preventRecursion = false)
    {
        idleModeActive = active;

        if (active)
        {
            ModeTracker.Initialise();
        }
        else
        {
            ModeTracker.DisableIdleMode();
        }

        if (preventRecursion)
        {
            return;
        }
        MockConsole.viewModel.IdleModeActive = GetIdleModeActive();
    }
    
    public static bool GetIdleModeActive()
    {
        if (idleModeActive == null)
        {
            idleModeActive =
                Environment.GetEnvironmentVariable("IdleMode", EnvironmentVariableTarget.User)?.Equals("On") ?? false;
        }
        if (!idleModeActive ?? false)
        {
            MockConsole.WriteLine("WARNING: Idle Mode is off, the Station will not go into Idle mode.");
        }
        
        return idleModeActive ?? false;
    }
}
