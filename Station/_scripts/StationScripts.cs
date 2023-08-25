using System;
using System.Timers;
using System.Threading;

namespace Station
{
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
                        Manager.SendResponse(source, "Station", "SetValue:gameName:" + url);
                        Manager.SendResponse("Android", "Station", "SetValue:gameId:");
                    }
                }
            }
            else if (additionalData.Equals("StartVR"))
            {
                //startVRSession();
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
                Manager.wrapperManager?.ActionHandler("Stop");
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
                Logger.WriteLog("Unidentified command", MockConsole.LogLevel.Error);
            }
        }

        /// <summary>
        /// Stop all processes that are associated with a VR session, wait until the processes are cleared and then restart
        /// the necessary programs for a new VR session.
        /// </summary>
        /// <returns></returns>
        public static void RestartVRSession()
        {
            Manager.SendResponse("Android", "Station", "SetValue:status:On");
            if (!processing)
            {
                processing = true;
                Manager.SendResponse("Android", "Station", "SetValue:status:On");
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

            void timerElapsed(object? obj, ElapsedEventArgs args)
            {
                if (tokenSource is null) return;
                if (tokenSource.IsCancellationRequested) return;

                EndVRSession();

                //Shut down the server first, so the NUC cannot send off any more Pings
                Manager.StopServer();
                Manager.SendResponse(source, "Station", "SetValue:status:Off");
                Manager.SendResponse(source, "Station", "SetValue:gameName:");
                Manager.SendResponse(source, "Station", "SetValue:gameId:");
            }

            timer.Elapsed += timerElapsed;
            timer.Enabled = true;
            timer.AutoReset = false;
        }

        /// <summary>
        /// Stop all processes that are associated with a VR session.
        /// </summary>
        public static void EndVRSession()
        {
            Manager.SendResponse("Android", "Station", "SetValue:status:On");
            Manager.wrapperManager?.ActionHandler("Session", "Stop");
            Manager.SendResponse("Android", "Station", "SetValue:session:Ended");
        }
    }
}
