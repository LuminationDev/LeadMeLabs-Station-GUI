
namespace Station.Components._enums;

/// <summary>
/// Enums that describe the Station Mode.
/// </summary>
public enum Headset
{
    [Description("The first version of the Vive VR headsets.")]
    [Value("VivePro1")]
    VivePro1,
    
    [Description("The second generation of the Vive VR headsets.")]
    [Value("VivePro2")]
    VivePro2,
    
    [Description("A backwards compatible version of the Vive Business Streaming.")]
    [Value("ViveFocus3")]
    ViveFocus,
    
    [Description("Vive Business Streaming compatible headsets. (Vive focus 3 and Vive XR Elite")]
    [Value("ViveBusinessStreaming")]
    ViveBusinessStreaming,
    
    [Description("A direct connection method between a headset and SteamVR.")]
    [Value("SteamLink")]
    SteamLink
}

