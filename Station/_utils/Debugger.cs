namespace Station._utils;

public static class Debugger
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
    public static bool autoStartVrPrograms = true;
    public static bool GetAutoStart()
    {
        if (!autoStartVrPrograms)
        {
            MockConsole.WriteLine("WARNING: Will not launch SteamVR, Auto Start VR Software set to 'No' in Debug Panel.");
        }
        
        return autoStartVrPrograms;
    }
}
