using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace Station
{
    public class VivePro2 : VrHeadset
    {
        private static HMDStatus openVRStatus; //Determined by OpenVR
        private static HMDStatus viveStatus; //Determined by Vive Logs
        private Timer? timer;
        private static bool minimising = false;

        public HMDStatus GetConnectionStatus()
        {
            return viveStatus;
        }

        public void SetOpenVRStatus(HMDStatus status)
        {
            openVRStatus = status;
        }

        public List<string> GetProcessesToQuery()
        {
            return new List<string> { "vrmonitor", "steam", "LhStatusMonitor" };
        }

        private string[] GetProcessesToMinimize()
        {
            return new[] { "vrmonitor", "steam", "LhStatusMonitor", "WaveConsole", "steamwebhelper" };
        }

        public void StartVrSession()
        {
            //Bail out if Steam and SteamVR are already running
            if(QueryMonitorProcesses())
            {
                return;
            }

            CommandLine.KillSteamSigninWindow();
            SteamConfig.VerifySteamConfig();
            CommandLine.StartProgram(SessionController.steam, "-noreactlogin -login " + 
                Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
                Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/1635730"); //Open up steam and run vive console

            if (!minimising)
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

        public void MonitorVrConnection()
        {
            Process[] vivePro2Connector = Process.GetProcessesByName("WaveConsole");
            Process[] viveStatusMonitor = Process.GetProcessesByName("LhStatusMonitor");
            if (vivePro2Connector.Length > 0)
            {
                foreach (var process in vivePro2Connector)
                {
                    if (process.MainWindowTitle.Equals("VIVE Console"))
                    {
                        if (viveStatus == HMDStatus.Connected)
                        {
                            SessionController.PassStationMessage("MessageToAndroid,LostHeadset");
                        }
                        viveStatus = HMDStatus.Lost;
                    }
                }
            }
            if (viveStatusMonitor.Length > 0)
            {
                if (viveStatus == HMDStatus.Lost)
                {
                    SessionController.PassStationMessage("MessageToAndroid,FoundHeadset");
                }

                viveStatus = HMDStatus.Connected;
            }
            viveStatus = HMDStatus.Lost;
        }

        /// <summary>
        /// Kill off the Steam VR process.
        /// </summary>
        public void StopProcessesBeforeLaunch()
        {
            //Not currently required for VivePro2
        }
    }
}
