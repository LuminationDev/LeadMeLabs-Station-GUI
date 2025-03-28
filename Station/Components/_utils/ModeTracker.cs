using System;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._enums;
using Station.Components._managers;
using Station.Components._notification;
using Station.Components._overlay;
using Station.Components._profiles;
using Station.Components._segment;
using Station.Components._segment._classes;
using Station.Components._utils._steamConfig;
using Station.MVC.Controller;

namespace Station.Components._utils;

public static class ModeTracker
{
    //1 hour timeout (60 minutes) * (60 seconds) * (1000 milliseconds)
    private const int Timeout = 60 * 60 * 1000;
    
    /// <summary>
    /// A private enum to track the different Station modes.
    /// </summary>
    private enum Mode
    {
        Normal,
        Idle
    }

    private static Mode CurrentMode { get; set; } = Mode.Normal;
    private static Timer? idleCheck;
    private static bool exitingIdleMode;

    /// <summary>
    /// Check if any experience messages should wait as the system is still turning on.
    /// </summary>
    /// <returns>A bool representing if the Station is currently attempting to exit Idle mode.</returns>
    public static bool GetExitingIdleMode()
    {
        return exitingIdleMode;
    }

    /// <summary>
    /// Track when the Station is in idle mode.
    /// </summary>
    /// <returns>A bool representing if the Station is currently in Idle mode.</returns>
    public static bool IsIdle()
    {
        return CurrentMode == Mode.Idle;
    }

    public static void Initialise()
    {
        // Do not engage Idle mode if the Station mode is anything other than VR
        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            MockConsole.WriteLine("Station is not in VR mode, Idle mode is not applicable.", Enums.LogLevel.Normal);
            return;
        }
        
        idleCheck?.Dispose();

        CurrentMode = Mode.Normal;
        idleCheck = new Timer(OnTimerCallback, null, Timeout, System.Threading.Timeout.Infinite);
        Logger.WriteLog("Idle mode timer started.", Enums.LogLevel.Normal);
    }

    /// <summary>
    /// Switch between Idle mode or Normal mode, this has been manually sent by the tablet.
    /// </summary>
    public static void ToggleIdleMode(string value)
    {
        switch (value)
        {
            case "idle":
                EnableIdleMode();
                break;
            
            case "normal" when CurrentMode == Mode.Idle:
                _ = ResetTimer();
                break;
            
            default:
                Logger.WriteLog($"ModeTracker - ToggleIdleMode: Mode is already - {value}", Enums.LogLevel.Normal);
                break;
        }
    }
    
    private static void OnTimerCallback(object? state)
    {
        //An experience is active
        if (WrapperManager.currentWrapper?.GetCurrentExperienceName()?.Length > 0)
        {
            Logger.WriteLog($"ModeTracker - OnTimerCallback() Active process detected: {WrapperManager.currentWrapper.GetCurrentExperienceName()}", Enums.LogLevel.Normal);
            idleCheck?.Change(Timeout, System.Threading.Timeout.Infinite);
            return;
        }
        
        //Already in idle mode
        if (CurrentMode == Mode.Idle) return;
        
        EnableIdleMode();
    }
    
    /// <summary>
    /// The Station has not had any interaction for x amount of time. Close any VR applications and update the Status
    /// for the NUC & Tablet. On exit of Idle mode the VR applications are started again.
    /// </summary>
    private static async void EnableIdleMode()
    {
        Logger.WriteLog("Station is entering Idle mode.", Enums.LogLevel.Normal);
        CurrentMode = Mode.Idle;
        
        //Update the status
        SessionController.CurrentState = State.Idle;
        StateController.UpdateStateValue("status", "Idle");
        
        // TODO - Enable this when we start using idle mode
        // MessageController.SendResponse("NUC", "Analytics", "EnterIdleMode");
        
        await WrapperManager.StopCommonProcesses();
    }

    /// <summary>
    /// The Station has been interacted with, exit idle mode and restart the VR processes.
    /// </summary>
    private static async Task<bool> ExitIdleMode()
    {
        new Thread(() => { OverlayManager.OverlayThreadManual("Exiting Idle Mode"); }).Start();

        Logger.WriteLog("Station is exiting Idle mode.", Enums.LogLevel.Normal);
        
        //Update the status
        SessionController.CurrentState = State.ExitIdle;
        StateController.UpdateStateValue("status", "On");

        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            return await WaitForVr();
        }
        
        // Check if there are steam details as the Station may be non-VR with a Steam account
        ContentProfile? contentProfile = Profile.CastToType<ContentProfile>(SessionController.StationProfile);
        if (contentProfile != null && contentProfile.DoesProfileHaveAccount("Steam"))
        {
            SessionController.StationProfile?.StartSession();
            OverlayManager.SetText("Launching software");
            WrapperManager.WaitForSteamProcess();
        }

        await Task.Delay(2500);
            
        OverlayManager.SetText("Ready for use");
        await Task.Delay(2500);
        
        OverlayManager.ManualStop();
        exitingIdleMode = false;
        return true;
    }

    /// <summary>
    /// Wait for the VR processes to start up again, this includes the headset management software and the steam client.
    /// </summary>
    /// <returns></returns>
    private static async Task<bool> WaitForVr()
    {
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return false;
            
        // This must be checked before the VR processes are restarted
        RoomSetup.CompareRoomSetup(); 

        //Reset the VR device statuses
        vrProfile.VrHeadset.GetStatusManager().ResetStatuses();
            
        SessionController.StationProfile?.StartSession();

        // Check if there are steam details as the Station may be non-VR without a Steam account
        WrapperManager.WaitForVrProcesses();
            
        OverlayManager.SetText("Launching software");
        
        //Wait for OpenVR to be available
        bool headsetSoftware = await Helper.MonitorLoop(() => ProcessManager.GetProcessesByName(vrProfile.VrHeadset.GetHeadsetManagementProcessName()).Length == 0, 20);
        if (!headsetSoftware)
        {
            ScheduledTaskQueue.EnqueueTask(() => SessionController.UpdateState(State.ErrorSteamVr), TimeSpan.FromSeconds(1));
            ScheduledTaskQueue.EnqueueTask(() =>
            {
                SegmentEvent segmentEvent = new SegmentStationEvent(
                    SegmentConstants.EventSteamVRError
                );
                Station.Components._segment.Segment.TrackAction(segmentEvent);
            }, TimeSpan.FromSeconds(1));
        }
        
        await Task.Delay(6000);
            
        OverlayManager.SetText("Ready for use");
        await Task.Delay(2500);
        
        OverlayManager.ManualStop();
        exitingIdleMode = false;
        return headsetSoftware;
    }
    
    /// <summary>
    /// An action has occurred, reset the timer back to the start. Exit idle mode if necessary.
    /// </summary>
    public static async Task<bool> ResetTimer()
    {
        //Track if exiting idle mode was successful
        bool success = true;
        
        if (CurrentMode != Mode.Normal)
        {
            exitingIdleMode = true;
            success = await ExitIdleMode();
            CurrentMode = Mode.Normal;
        }
        
        // Change the timer's due time to the initial interval
        idleCheck?.Change(Timeout, System.Threading.Timeout.Infinite);

        return success;
    }

    /// <summary>
    /// Reset the mode back to normal and restart the timer if an outside action has caused the VR systems to restart.
    /// I.e. The 'Restart VR System' command from the tablet.
    /// </summary>
    public static void ResetMode()
    {
        CurrentMode = Mode.Normal;
        idleCheck?.Change(Timeout, System.Threading.Timeout.Infinite);
    }

    /// <summary>
    /// Disable Idle mode, stop and dispose of the timer.
    /// </summary>
    public static void DisableIdleMode()
    {
        CurrentMode = Mode.Normal;
        idleCheck?.Dispose();
        
        Logger.WriteLog("Idle mode disabled", Enums.LogLevel.Normal);
    }
}
