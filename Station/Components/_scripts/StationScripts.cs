using System.Collections.Generic;
using System.Threading;
using System.Timers;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._overlay;
using Station.Components._utils;
using Station.MVC.Controller;

namespace Station.Components._scripts;

public static class StationScripts
{
    /// <summary>
    /// Track if a restart is in progress as to not que up multiple.
    /// </summary>
    public static bool processing = false;
    private static CancellationTokenSource? tokenSource = null;

    /// <summary>
    /// Determine what command is suppose to be run and execute the appropriate script.
    /// </summary>
    /// <param name="source">A string of where the command originated from</param>
    /// <param name="additionalData">A string containing the necessary information to run a specific command</param>
    /// <returns>A string representing the outcome of the action.</returns>
    public static void Execute(string source, string additionalData)
    {
        string[] dataTokens = additionalData.Split(':', 2);
        if (dataTokens.Length == 0) return; 

        switch (dataTokens[0])
        {
            case "RestartVR":
                RestartVrSession();
                break;
            case "EndVR":
                EndVrSession();
                break;
            case "Restart":
                ShutdownOrRestartCommand(source, "restart");
                break;
            case "Shutdown":
                ShutdownOrRestartCommand(source, "shutdown");
                break;
            case "CancelShutdown":
                CommandLine.CancelShutdown();
                tokenSource?.Cancel();
                break;
            case "StopGame":
                MainController.wrapperManager?.ActionHandler("Stop");
                break;
            case "IdentifyStation":
                OverlayManager.OverlayThread();
                break;
            case "UploadLogFile":
                _ = CommandLine.UploadLogFile();
                break;
            default:
                Logger.WriteLog("Unidentified command", Enums.LogLevel.Info);
                break;
        }
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session, wait until the processes are cleared and then restart
    /// the necessary programs for a new VR session.
    /// </summary>
    /// <returns></returns>
    private static void RestartVrSession()
    {
        StateController.UpdateStateValue("status", "On");
        if (!processing)
        {
            processing = true;
            StateController.UpdateStateValue("status", "On");
            MainController.wrapperManager?.ActionHandler("Session", "Restart");
        }
        else
        {
            Logger.WriteLog("Processing...", Enums.LogLevel.Verbose);
        }
    }

    /// <summary>
    /// Depending on the received command, shutdown or restart the Station.
    /// </summary>
    private static void ShutdownOrRestartCommand(string source, string type)
    {
        int cancelTime = 10000; // give the user 10 seconds to cancel the shutdown
        int actualCancelTime = 15; // time before the computer actually shuts down
        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_APPLIANCE))
        {
            cancelTime = 0;
            actualCancelTime = 0;
        }

        if(type.Equals("shutdown"))
        {
            CommandLine.ShutdownStation(actualCancelTime);
        }
        else if (type.Equals("restart"))
        {
            CommandLine.RestartStation(actualCancelTime);
        }

        tokenSource = new CancellationTokenSource();
        var timer = new System.Timers.Timer(cancelTime);

        timer.Elapsed += TimerElapsed;
        timer.Enabled = true;
        timer.AutoReset = false;
        return;

        void TimerElapsed(object? obj, ElapsedEventArgs args)
        {
            if (tokenSource is null) return;
            if (tokenSource.IsCancellationRequested) return;

            EndVrSession();

            //Shut down the server first, so the NUC cannot send off any more Pings
            MainController.StopServer();
            
            Dictionary<string, object?> stateValues = new()
            {
                { "status", "Off" },
                { "state", "" },
                { "gameName", "" },
                { "gameId", "" }
            };
            
            StateController.UpdateStatusBunch(stateValues);
        }
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session.
    /// </summary>
    private static void EndVrSession()
    {
        MainController.wrapperManager?.ActionHandler("Session", "Stop");
        //Old set value method as this goes directly to the tablet through the NUC - nothing is saved temporarily
        MessageController.SendResponse("Android", "Station", "SetValue:session:Ended");
        StateController.UpdateStateValue("status", "On");
    }
}
