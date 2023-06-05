using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace Station
{
    public class VivePro2 : VrHeadset
    {
        private Timer? timer;
        private static bool minimising = false;

        public List<string> GetProcessesToQuery()
        {
            return new List<string> { "vrmonitor", "steam", "LhStatusMonitor" };
        }

        private string[] GetProcessesToMinimize()
        {
            return new[] { "vrmonitor", "steam", "LhStatusMonitor", "WaveConsole" };
        }

        public void StartVrSession()
        {
            //TODO this may not be correct
            //Bail out if Steam and SteamVR are already running
            if(QueryMonitorProcesses())
            {
                return;
            }

            CommandLine.KillSteamSigninWindow();
            CommandLine.StartProgram(SessionController.steam, "-noreactlogin -login " + Environment.GetEnvironmentVariable("SteamUserName") + " " + Environment.GetEnvironmentVariable("SteamPassword") + " steam://rungameid/1635730"); //Open up steam and run vive console

            if (!minimising && !(Environment.GetEnvironmentVariable("OfflineMode") ?? "false").Equals("true"))
            {
                minimising = true;
                timer = new Timer(5000); // every 5 seconds try to minimize the processes
                int attempts = 0;

                void TimerElapsed(object? obj, ElapsedEventArgs args)
                {
                    MinimizeVrProcesses();
                    attempts++;
                    if (attempts > 6) // after 30 seconds, we can stop
                    {
                        timer.Stop();
                        minimising = false;
                    }
                }
                timer.Elapsed += TimerElapsed;
                timer.AutoReset = true;
                timer.Enabled = true;
            }
            
            if ((Environment.GetEnvironmentVariable("OfflineMode") ?? "false").Equals("true"))
            {
                SteamWrapper.HandleOfflineSteam();
            }
        }

        /// <summary>
        /// Stop trying to minimise Steam and Vive
        /// </summary>
        public void StopTimer()
        {
            timer?.Stop();
        }

        /// <summary>
        /// Query the running processes to see if Steam or SteamVR is currently running.
        /// </summary>
        /// <returns></returns>
        public bool QueryMonitorProcesses()
        {
            HashSet<string> list = new();
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                if (GetProcessesToQuery().Contains(process.ProcessName))
                {
                    list.Add(process.ProcessName);
                }
            }

            return list.Count == GetProcessesToQuery().Count;
        }

        public void MinimizeVrProcesses()
        {
            Logger.WriteLog("minimizing processes", MockConsole.LogLevel.Verbose);
            foreach (string processName in GetProcessesToMinimize())
            {
                Process[] processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes)
                {
                    Logger.WriteLog("minimizing: " + process.ProcessName, MockConsole.LogLevel.Verbose);
                    WindowManager.MinimizeProcess(process);
                }
            }
        }

        public string MonitorVrConnection(string currentViveStatus)
        {
            Process[] vivePro2Connector = Process.GetProcessesByName("WaveConsole");
            Process[] viveStatusMonitor = Process.GetProcessesByName("LhStatusMonitor");
            if (vivePro2Connector.Length > 0)
            {
                foreach (var process in vivePro2Connector)
                {
                    if (process.MainWindowTitle.Equals("VIVE Console"))
                    {
                        if (currentViveStatus.Equals("CONNECTED"))
                        {
                            SessionController.PassStationMessage("MessageToAndroid,LostHeadset");
                        }
                        return "TERMINATED";
                    }
                }
            }
            if (viveStatusMonitor.Length > 0)
            {
                if (currentViveStatus.Equals("TERMINATED"))
                {
                    SessionController.PassStationMessage("MessageToAndroid,FoundHeadset");
                }

                return "CONNECTED";
            }
            return "TERMINATED";
        }

        /// <summary>
        /// Kill off the Steam VR process.
        /// </summary>
        public void StopProcessesBeforeLaunch()
        {
        }
    }
}
