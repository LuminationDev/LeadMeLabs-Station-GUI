using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._managers;
using Station.Components._notification;
using Station.Components._overlay;
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
    
    private static Mode CurrentMode { get; set; }
    private static Timer? idleCheck;

    public static void Initialise()
    {
        idleCheck?.Dispose();

        CurrentMode = Mode.Normal;
        idleCheck = new Timer(OnTimerCallback, null, Timeout, System.Threading.Timeout.Infinite);
        Logger.WriteLog("Idle mode timer started.", MockConsole.LogLevel.Normal);
    }
    
    private static void OnTimerCallback(object? state)
    {
        //An experience is active
        if (WrapperManager.currentWrapper?.GetCurrentExperienceName()?.Length > 0)
        {
            Logger.WriteLog($"ModeTracker - OnTimerCallback() Active process detected: {WrapperManager.currentWrapper?.GetCurrentExperienceName()}", MockConsole.LogLevel.Normal);
            idleCheck?.Change(Timeout, System.Threading.Timeout.Infinite);
            return;
        }
        
        //Already in idle mode
        if (CurrentMode == Mode.Idle) return;
        
        CurrentMode = Mode.Idle;
        EnableIdleMode();
    }
    
    /// <summary>
    /// The Station has not had any interaction for x amount of time. Close any VR applications and update the Status
    /// for the NUC & Tablet. On exit of Idle mode the VR applications are started again.
    /// </summary>
    private static void EnableIdleMode()
    {
        Logger.WriteLog("Station is entering Idle mode.", MockConsole.LogLevel.Normal);
        
        //Update the status
        SessionController.CurrentState = "Idle Mode...";
        //TODO This currently stops the Tablets Single Station Fragment from working - implement this after the next tablet update
        // MessageController.SendResponse("Android", "Station", "SetValue:status:Idle");
        
        //Exit VR applications
        WrapperManager.StopCommonProcesses();
    }

    /// <summary>
    /// The Station has been interacted with, exit idle mode and restart the VR processes.
    /// </summary>
    private static async Task<bool> ExitIdleMode()
    {
        new Thread(() => { OverlayManager.OverlayThreadManual("Exiting Idle Mode"); }).Start();

        Logger.WriteLog("Station is exiting Idle mode.", MockConsole.LogLevel.Normal);
        
        //Update the status
        SessionController.CurrentState = "Exiting Idle Mode";
        MessageController.SendResponse("Android", "Station", "SetValue:status:On");

        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            //Start VR applications
            await WrapperManager.RestartVrProcesses();
        
            OverlayManager.SetText("Waiting for SteamVR");
        
            //Wait for OpenVR to be available
            bool steamvr = await Helper.MonitorLoop(() => ProcessManager.GetProcessesByName("vrmonitor").Length == 0, 20);
            if (!steamvr)
            {
                JObject message = new JObject
                {
                    { "action", "SoftwareState" },
                    { "value", "SteamVR Error" }
                };
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(1));
            }
        
            await Task.Delay(2500);
            
            OverlayManager.SetText("Ready for use");
            await Task.Delay(2500);
        
            OverlayManager.ManualStop();

            return steamvr;
        }
        else
        {
            await Task.Delay(2500);
            
            OverlayManager.SetText("Ready for use");
            await Task.Delay(2500);
        
            OverlayManager.ManualStop();

            return true;
        }
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
        
        Logger.WriteLog("Idle mode disabled", MockConsole.LogLevel.Normal);
    }
}
