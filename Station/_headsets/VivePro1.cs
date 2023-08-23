using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;
using LeadMeLabsLibrary.Station;

namespace Station
{
    public class VivePro1 : VrHeadset
    {
        private Statuses Statuses { get; }

        private Timer? timer;
        private static bool minimising = false;

        public VivePro1()
        {
            Statuses = new Statuses();
        }

        public Statuses GetStatusManager()
        {
            return Statuses;
        }

        /// <summary>
        /// If the headset is managed by more than just OpenVR return the management software connection
        /// status. In this case it is managed by Vive Wireless.
        /// </summary>
        /// <returns></returns>
        public DeviceStatus GetHeadsetManagementSoftwareStatus()
        {
            return Statuses.SoftwareStatus;
        }
        
        /// <summary>
        /// Return the process name of the headset management software
        /// </summary>
        /// <returns></returns>
        public string GetHeadsetManagementProcessName()
        {
            return "HtcConnectionUtility";
        }

        /// <summary>
        /// Collect the connection status of the headset from the headset's specific management software. In this case it
        /// is Vive Wireless.
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
            return new List<string> { "vrmonitor", "steam", "HtcConnectionUtility", "steamwebhelper" };
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
            CommandLine.StartProgram(SessionController.steam, " -login " + 
                Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
                Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/250820"); //Open up steam and run steamVR
            CommandLine.StartProgram(SessionController.vive); //Start VireWireless up

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
            foreach (string processName in GetProcessesToQuery())
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
            var directory = new DirectoryInfo(@"C:\ProgramData\VIVE Wireless\ConnectionUtility\Log");
            var file = directory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .First();
            ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.Unicode);
            IEnumerator<string> enumerator = reverseLineReader.GetEnumerator();
            do
            {
                string current = enumerator.Current;
                if (current == null)
                {
                    continue;
                }
                if (current.Contains("Terminated"))
                {
                    enumerator.Dispose();
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Off);
                    return;
                }

                if (current.Contains("Connection Status set to"))
                {
                    if ((Statuses.SoftwareStatus == DeviceStatus.Connected || Statuses.SoftwareStatus == DeviceStatus.Off) && current.Contains("CONNECTION_STATUS_SCANNING"))
                    {
                        Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                    }
                    else if ((Statuses.SoftwareStatus == DeviceStatus.Lost || Statuses.SoftwareStatus == DeviceStatus.Off) && current.Contains("CONNECTION_STATUS_CONNECTED"))
                    {
                        Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
                    }
                    enumerator.Dispose();
                }
            } while (enumerator.MoveNext());
        }

        /// <summary>
        /// Kill off the Steam VR process.
        /// </summary>
        public async void StopProcessesBeforeLaunch()
        {
            CommandLine.QueryVRProcesses(new List<string> { "vrmonitor" }, true);
            
            await Task.Delay(3000);
        }
    }
}
