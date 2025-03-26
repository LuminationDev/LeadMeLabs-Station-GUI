namespace Station.Components._enums;

/// <summary>
/// Represents the different states the Station may be in. The NUC and tablet handle the values of these enums so placing
/// them in a central location eliminates the likelihood of spelling mistakes over different classes.
/// </summary>
public enum State
{
    [Description("The default state.")]
    [Value("")]
    Base,
    
    [Description("A Station is opening up the required software.")]
    [Value("Launching Software")]
    Launching,
    
    [Description("A Station is loading the environment variables.")]
    [Value("Initialising configuration")]
    Initialising,
    
    [Description("A Station is starting its initial processes.")]
    [Value("Starting processes")]
    StartProcess,
    
    [Description("A Station is restarting its initial processes.")]
    [Value("Restarting Processes")]
    RestartProcess,
    
    [Description("A Station is collecting its local experiences.")]
    [Value("Loading experiences")]
    Experiences,
    
    [Description("A Station is waiting for a headset to connect.")]
    [Value("Awaiting headset connection...")]
    Awaiting,
    
    [Description("A Station's headset has disconnected while an experience is open.")]
    [Value("Lost headset connection")]
    Lost,
    
    [Description("A Station has all devices connected and ready.")]
    [Value("Ready to go")]
    Ready,
    
    [Description("A Station is running windows updates.")]
    [Value("Updating...")]
    Updating,
    
    [Description("A Station is in debug mode.")]
    [Value("Debug Mode")]
    Debug,
    
    [Description("A Station is starting its VR processes.")]
    [Value("Starting VR processes")]
    StartVrProcess,
    
    [Description("A Station is stopping its VR processes.")]
    [Value("Shutting down VR processes")]
    StopVrProcess,
    
    [Description("A Station is connecting to Steam VR.")]
    [Value("Connecting SteamVR")]
    ConnectSteamVr,
    
    [Description("A Station has encountered an error with Steam VR.")]
    [Value("SteamVR Error")]
    ErrorSteamVr,
    
    [Description("A Station could not open the Steam client.")]
    [Value("Error: Steam could not open")]
    ErrorSteam,
    
    [Description("A Station could not open Vive.")]
    [Value("Error: Vive could not open")]
    ErrorVive,
    
    [Description("A Station is restarting Steam VR.")]
    [Value("Restarting SteamVR")]
    RestartSteamVr,
    
    [Description("A Station is in idle mode.")]
    [Value("Idle Mode")]
    Idle,
    
    [Description("A Station is exiting idle mode.")]
    [Value("Exiting Idle Mode")]
    ExitIdle,
    
    [Description("A Station is checking if a network is available.")]
    [Value("Checking network")]
    Network,
    
    [Description("A Station is running a QA check.")]
    [Value("Running QA")]
    Qa,
}
