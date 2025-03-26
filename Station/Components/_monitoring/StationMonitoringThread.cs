using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Sentry;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._notification;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;

namespace Station.Components._monitoring;

public static class StationMonitoringThread
{
    private static Thread? monitoringThread;
    private static DateTime latestHighTemperatureWarning = DateTime.Now;
    private static System.Timers.Timer? timer;

    /// <summary>
    /// Start a new thread with the Vive monitor check.
    /// </summary>
    public static void InitializeMonitoring()
    {
        monitoringThread = new Thread(InitializeRespondingCheck);
        monitoringThread.Start();
    }

    public static void StopMonitoring()
    {
        monitoringThread?.Interrupt();
        timer?.Stop();
    }

    /// <summary>
    /// Start checking that VR applications and current Steam app are responding
    /// Will check every 5 seconds
    /// </summary>
    private static void InitializeRespondingCheck()
    {
        timer = new System.Timers.Timer(3000);
        timer.AutoReset = true;
        timer.Elapsed += CallCheck;
        timer.Start();
    }

    private static int numberOfChecks = 0;
    /// <summary>
    /// Calls a function to check that all required VR processes are running
    /// If they are not sends a messages to the NUC/Tablet that there are tasks
    /// that aren't responding
    /// </summary>
    private static void CallCheck(Object? source, System.Timers.ElapsedEventArgs e)
    {
        //Check for any actions that are required to be done at a certain time.
        if (DeviceControl.CheckForTimedActions())
        {
            return;
        }

        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            new Task(OpenVrCheck).Start(); //Perform as separate task in case SteamVR is restarting.
        }
        TemperatureCheck();
        
        // Only check if a VR profile or the Content profile's account list has 'Steam' in it
        // Safe cast for potential content profile
        ContentProfile? contentProfile = Profile.CastToType<ContentProfile>(SessionController.StationProfile);

        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR) || (contentProfile != null && contentProfile.DoesProfileHaveAccount("Steam")))
        {
            NewSteamProcessesCheck();
        }

        Logger.WorkQueue();
    }

    /// <summary>
    /// Checks the third party headset management software and if OpenVR has been initialised, 
    /// if not attempt to initialise it and query if there are any running application.
    /// </summary>
    private static void OpenVrCheck()
    {
        // If in Idle mode to not attempt the check
        if (ModeTracker.IsIdle()) return;
        
        ExternalSoftwareCheck();
        
        //An early exit if the vrmonitor (SteamVR) process is not currently running
        if (ProcessManager.GetProcessesByName("vrmonitor").Length == 0) return;

        //Attempt to contact OpenVR, if this fails check the logs for errors
        if (MainController.openVrManager?.InitialiseOpenVr() ?? false)
        {
            MainController.openVrManager.QueryCurrentApplication();
            MainController.openVrManager.StartDeviceChecks(); //Start a loop instead of continuously checking
        } 
        else
        {
            SteamScripts.CheckForSteamLogError();
        }
    }

    /// <summary>
    /// Check the third party headset management software for the current connection.
    /// </summary>
    private static void ExternalSoftwareCheck()
    {
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return;

        //An early exit if the monitoring process is not currently running.
        if (ProcessManager.GetProcessesByName(vrProfile.VrHeadset.GetHeadsetManagementProcessName()).Length == 0)
        {
            vrProfile.VrHeadset.GetStatusManager().UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
            return;
        }

        vrProfile.VrHeadset.MonitorVrConnection();
        MockConsole.WriteLine(
            $"VR SoftwareStatus: {vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus()}", 
            Enums.LogLevel.Debug);
    }

    private static async void NewSteamProcessesCheck()
    {
        Process[] steamProcesses = ProcessManager.GetProcessesByName("steamwebhelper");
        foreach (var process in steamProcesses)
        {
            if (string.IsNullOrEmpty(process.MainWindowTitle)) continue;
            if (process.MainWindowTitle.Equals("Valve Hardware Survey"))
            {
                try
                {
                    StationCommandLine.SetForegroundWindow(process.MainWindowHandle.ToInt32());
                    await Task.Delay(10);
                    StationCommandLine.SendKeysToActiveWindow("{Tab}{Tab}{Enter");
                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                }
            }
            if (!process.MainWindowTitle.Equals("Steam")) continue;

            if (App.steamProcessId == process.Id) continue;
            
            try
            {
                App.windowEventTracker?.Subscribe("Steam", null);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"NewSteamProcessesCheck - Sentry Exception: {e}", Enums.LogLevel.Error);
                SentrySdk.CaptureException(e);
            }
                
            App.steamProcessId = process.Id;
        }
    }

    /// <summary>
    /// Performs a temperature check to monitor for high temperature conditions.
    /// Increments the count of temperature checks and evaluates conditions based on time and checks count.
    /// If enough time has passed and a certain number of checks have been performed, retrieves the current temperature.
    /// If the temperature exceeds 90 degrees, sends a response to the "Android" endpoint indicating "HighTemperature",
    /// logs the high temperature event, captures the event using Sentry for error tracking,
    /// and updates the timestamp for the latest high temperature warning.
    /// </summary>
    private static void TemperatureCheck()
    {
        numberOfChecks++;

        float? temperature = 0;
        if (DateTime.Now > latestHighTemperatureWarning.AddMinutes(5) && (numberOfChecks == 20))
        {
            numberOfChecks = 0;
            temperature = Temperature.GetTemperature();
        }

        if (temperature > 90)
        {
            MessageController.SendResponse("Android", "Station", "HighTemperature");
            SentrySdk.CaptureMessage("High temperature detected (" + temperature + ") at: " + Helper.GetLabLocationWithStationId());
            Logger.WriteLog("High temperature detected (" + temperature + ") at: " + Helper.GetLabLocationWithStationId(), Enums.LogLevel.Error);
            latestHighTemperatureWarning = DateTime.Now;
        }
    }
}
