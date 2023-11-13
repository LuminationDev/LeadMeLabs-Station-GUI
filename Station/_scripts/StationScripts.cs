using System;
using System.Threading;
using System.Timers;
using Newtonsoft.Json.Linq;

namespace Station._scripts;

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
    /// <param name="source">A string containing the necessary information to run a specific command</param>
    /// <param name="additionalData">A string containing the necessary information to run a specific command</param>
    public static void Execute(string source, JObject additionalData)
    {
        if (additionalData.ContainsKey("URL"))
        {
            string? url = additionalData.GetValue("URL")?.ToString();
            if (url == null) return;
            
            if (!url.StartsWith("https://") && !url.StartsWith("http://"))
            {
                url = "https://" + url;
            }
            bool isValidUrl = Uri.IsWellFormedUriString(url, UriKind.Absolute);
            if (isValidUrl)
            {
                CommandLine.ExecuteBrowserCommand(url);
                
                JObject values = new JObject
                {
                    { "gameName", url },
                    { "gameId", "" }
                };
                JObject setValue = new() { { "SetValue", values } };
                Manager.SendMessage("NUC", "Station", setValue);
            }
            
        }
        else if (additionalData.ContainsKey("StartVR"))
        {
            //startVRSession();
        }
        else if (additionalData.ContainsKey("RestartVR"))
        {
            RestartVrSession();
        }
        else if (additionalData.ContainsKey("EndVR"))
        {
            EndVrSession();
        }
        else if (additionalData.ContainsKey("Restart"))
        {
            ShutdownOrRestartCommand(source, "restart");
        }
        else if (additionalData.ContainsKey("Shutdown"))
        {
            ShutdownOrRestartCommand(source, "shutdown");
        }
        else if (additionalData.ContainsKey("CancelShutdown"))
        {
            CommandLine.CancelShutdown();
            tokenSource?.Cancel();
        }
        else if (additionalData.ContainsKey("StopGame"))
        {
            Manager.wrapperManager?.ActionHandler("Stop");
        }
        else if (additionalData.ContainsKey("IdentifyStation"))
        {
            OverlayManager.OverlayThread();
        } else if (additionalData.ContainsKey("UploadLogFile"))
        {
            _ = CommandLine.UploadLogFile();
        }
        else
        {
            Logger.WriteLog("Unidentified command", MockConsole.LogLevel.Error);
        }
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session, wait until the processes are cleared and then restart
    /// the necessary programs for a new VR session.
    /// </summary>
    /// <returns></returns>
    private static void RestartVrSession()
    {
        JObject values = new JObject
        {
            { "status", "On" }
        };
        JObject setValue = new() { { "SetValue", values } };
        Manager.SendMessage("NUC", "Station", setValue);
        
        if (!processing)
        {
            processing = true;
            Manager.wrapperManager?.ActionHandler("Session", "Restart");
        }
        else
        {
            Logger.WriteLog("Processing...", MockConsole.LogLevel.Verbose);
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

        void TimerElapsed(object? obj, ElapsedEventArgs args)
        {
            if (tokenSource is null) return;
            if (tokenSource.IsCancellationRequested) return;

            EndVrSession();

            //Shut down the server first, so the NUC cannot send off any more Pings
            Manager.StopServer();
            
            JObject values = new JObject
            {
                { "status", "Off" },
                { "gameName", "" },
                { "gameId", "" }
            };
            JObject setValue = new() { { "SetValue", values } };
            Manager.SendMessage(source, "Station", setValue);
        }
    }

    /// <summary>
    /// Stop all processes that are associated with a VR session.
    /// </summary>
    private static void EndVrSession()
    {
        Manager.wrapperManager?.ActionHandler("Session", "Stop");
        
        JObject values = new JObject
        {
            { "status", "On" },
            { "session", "Ended" }
        };
        JObject setValue = new() { { "SetValue", values } };
        Manager.SendMessage("Android", "Station", setValue);
    }
}
