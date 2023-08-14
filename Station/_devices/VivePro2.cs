using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;

namespace Station
{
    public class VivePro2 : VrHeadset
    {
        public VrStatus VrStatus { private set; get; }

        private Timer? timer;
        private static bool minimising = false;

        public VivePro2() 
        {
            VrStatus = new VrStatus();
        }

        public VrStatus GetStatusManager()
        {
            return VrStatus;
        }

        /// <summary>
        /// If the headset is managed by more than just OpenVR return the management software connection
        /// status. In this case it is managed by Vive Console.
        /// </summary>
        /// <returns></returns>
        public DeviceStatus GetHeadsetManagementSoftwareStatus()
        {
            return VrStatus.SoftwareStatus;
        }

        /// <summary>
        /// Collect the connection status of the headset from the headset's specific management software. In this case it
        /// is Vive Console.
        /// </summary>
        /// <param name="wrapperType">A string of the Wrapper type that is being launched, required if the process
        /// needs to restart/start the VR session.</param>
        /// <returns>A bool representing the connection status.</returns>
        public bool WaitForConnection(string wrapperType)
        {
            return ViveScripts.WaitForVive(wrapperType).Result;
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
            if (QueryMonitorProcesses())
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
                        VrStatus.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                    }
                }
            }
            if (viveStatusMonitor.Length > 0)
            {
                VrStatus.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
            }
            VrStatus.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
        }

        /// <summary>
        /// Kill off the Steam VR process.
        /// </summary>
        public void StopProcessesBeforeLaunch()
        {
            //Not currently required for VivePro2
        }

        /// <summary>
        /// Restart the Vive Wireless. This reconnects lost or error state headsets without closing
        /// SteamVR (which may close an open experience).
        /// </summary>
        public async void RestartVive() //TODO this is not called anywhere yet
        {
            CommandLine.QueryVRProcesses(new List<string> { "LhStatusMonitor", "WaveConsole" }, true);
            SessionController.PassStationMessage($"ApplicationUpdate,Restarting Vive...");

            await Task.Delay(5000);

            CommandLine.StartProgram(SessionController.steam, "-noreactlogin -login " +
                Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " +
                Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/1635730");


            //Track the attempts
            int monitorAttempts = 0;
            int attemptLimit = 10;
            int delay = 2000;

            //Check the condition status (bail out after x amount)
            do
            {
                monitorAttempts++;
                await Task.Delay(delay);
            } while (Process.GetProcessesByName("LhStatusMonitor").Length == 0 && monitorAttempts < attemptLimit);

            // Connection bailed out, send a failure message
            if (monitorAttempts == attemptLimit)
            {
                SessionController.PassStationMessage($"ApplicationUpdate,Vive Error");
                return;
            }

            SessionController.PassStationMessage($"ApplicationUpdate,Vive Restarted");
        }
    }
}
