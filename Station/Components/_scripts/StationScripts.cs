using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using LeadMeLabsLibrary;
using Station.Components._commandLine;
using Station.Components._legacy;
using Station.Components._overlay;
using Station.Components._utils;
using Station.Components._version;
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
    /// Track if an experience is being launched.
    /// </summary>
    public static bool launchingExperience = false;

    /// <summary>
    /// Determine what command is suppose to be run and execute the appropriate script.
    /// </summary>
    /// <param name="command">A string containing the necessary information to run a specific command</param>
    /// <returns>A string representing the outcome of the action.</returns>
    public static void Execute(string source, string additionalData)
    {
        if (additionalData.StartsWith("URL"))
        {
            string[] urlCommand = additionalData.Split(':', 2);
            if (urlCommand.Length == 2)
            {
                string url = urlCommand[1];
                if (!url.StartsWith("https://") && !url.StartsWith("http://"))
                {
                    url = "https://" + url;
                }
                bool isValidUrl = Uri.IsWellFormedUriString(url, UriKind.Absolute);
                if (isValidUrl)
                {
                    CommandLine.ExecuteBrowserCommand(url);
                    
                    if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
                    {
                        LegacySetValue.SimpleSetValue("gameName", url);
                        LegacySetValue.SimpleSetValue("gameId", "");
                    }
                    else
                    {
                        Dictionary<string, object> stateValues = new()
                        {
                            { "gameName", url },
                            { "gameId", "" }
                        };
                        StateController.UpdateStatusBunch(stateValues);
                    }
                }
            }
        }
        else if (additionalData.Equals("RestartVR"))
        {
            RestartVRSession();
        }
        else if (additionalData.Equals("EndVR"))
        {
            EndVRSession();
        }
        else if (additionalData.Equals("Restart"))
        {
            ShutdownOrRestartCommand(source, "restart");
        }
        else if (additionalData.Equals("Shutdown"))
        {
            ShutdownOrRestartCommand(source, "shutdown");
        }
        else if (additionalData.Equals("CancelShutdown"))
        {
            CommandLine.CancelShutdown();
            tokenSource?.Cancel();
        }
        else if (additionalData.Equals("StopGame"))
        {
            MainController.wrapperManager?.ActionHandler("Stop");
        }
        else if (additionalData.StartsWith("IdentifyStation"))
        {
            OverlayManager.OverlayThread();
        } else if (additionalData.StartsWith("UploadLogFile"))
        {
            CommandLine.UploadLogFile();
        }
        else
        {
            Logger.WriteLog("Unidentified command", Enums.LogLevel.Info);
        }
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session, wait until the processes are cleared and then restart
    /// the necessary programs for a new VR session.
    /// </summary>
    /// <returns></returns>
    public static void RestartVRSession()
    {
        if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
        {
            LegacySetValue.SimpleSetValue("status", "On");
        }
        else
        {
            StateController.UpdateStateValue("status", "On");
        }
        if (!processing)
        {
            processing = true;
            if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
            {
                LegacySetValue.SimpleSetValue("status", "On");
            }
            else
            {
                StateController.UpdateStateValue("status", "On");
            }
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

            EndVRSession();

            //Shut down the server first, so the NUC cannot send off any more Pings
            MainController.StopServer();
            
            if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
            {
                LegacySetValue.StationOff("");
            }
            else
            {
                Dictionary<string, object> stateValues = new()
                {
                    { "status", "Off" },
                    { "state", "" },
                    { "gameName", "" },
                    { "gameId", "" }
                };
                
                StateController.UpdateStatusBunch(stateValues);
            }
        }
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session.
    /// </summary>
    public static void EndVRSession()
    {
        MainController.wrapperManager?.ActionHandler("Session", "Stop");
        //Old set value method as this goes directly to the tablet through the NUC - nothing is saved temporarily
        MessageController.SendResponse("Android", "Station", "SetValue:session:Ended");
        
        if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
        {
            LegacySetValue.SimpleSetValue("status", "On");
            
        }
        else
        {
            StateController.UpdateStateValue("status", "On");
        }
    }
}
