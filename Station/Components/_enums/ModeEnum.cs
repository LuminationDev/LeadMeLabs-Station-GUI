namespace Station.Components._enums;

/// <summary>
/// Enums that describe the Station Mode.
/// </summary>
public enum StationMode
{
    [Description("A VR Station - the current default.")]
    [Value("VR")]
    VirtualReality,
    
    [Description("A Station that is not meant to be interacted with besides turning off/on.")]
    [Value("Appliance")]
    Appliance,
    
    [Description("A non-vr Station that hosts content or a particular display")]
    [Value("Content")]
    Content,
    
    [Description("A single Station instance running on a standalone Pod system.")]
    [Value("Pod")]
    Pod
}
