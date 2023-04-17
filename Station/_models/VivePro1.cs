using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;

namespace Station
{
    public class VivePro1 : VrHeadset
    {
        private Timer? timer;

        public List<string> GetProcessesToQuery()
        {
            return new List<string> { "vrmonitor", "steam", "HtcConnectionUtility" };
        }

        public void StartVrSession()
        {
            //Bail out if Steam and SteamVR are already running
            if (QueryMonitorProcesses())
            {
                return;
            }

            CommandLine.KillSteamSigninWindow();
            CommandLine.startProgram(SessionController.steam, "-noreactlogin -login " + Environment.GetEnvironmentVariable("SteamUserName") + " " + Environment.GetEnvironmentVariable("SteamPassword") + " steam://rungameid/250820"); //Open up steam and run steamVR
            CommandLine.startProgram(SessionController.vive); //Start VireWireless up

            timer = new Timer(5000); // every 5 seconds try to minimize the processes
            int attempts = 0;

            void TimerElapsed(object? obj, ElapsedEventArgs args)
            {
                MinimizeVrProcesses();
                attempts++;
                if (attempts > 6) // after 30 seconds, we can stop
                {
                    timer.Stop();
                }
            }
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
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
            List<string> software = new() { "Steam", "SteamVR", "ViveConsole" };

            HashSet<string> list = new();
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                if (software.Contains(process.ProcessName))
                {
                    list.Add(process.ProcessName);
                }
            }

            return list.Count == software.Count;
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

        public string MonitorVrConnection(string currentViveStatus)
        {
            var directory = new DirectoryInfo(@"C:\ProgramData\VIVE Wireless\ConnectionUtility\Log");
            var file = directory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .First();
            ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.Unicode);
            IEnumerator<string> enumerator = reverseLineReader.GetEnumerator();
            Console.WriteLine(enumerator.Current);
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
                    return "Terminated";
                }

                if (current.Contains("Connection Status set to"))
                {
                    string previousViveStatus = (string)currentViveStatus.Clone();
                    if (previousViveStatus.Contains("CONNECTION_STATUS_CONNECTED") &&
                        current.Contains("CONNECTION_STATUS_SCANNING"))
                    {
                        SessionController.PassStationMessage("MessageToAndroid,LostHeadset");
                    }
                    else if (current.Contains("CONNECTION_STATUS_CONNECTED") &&
                        previousViveStatus.Contains("CONNECTION_STATUS_SCANNING"))
                    {
                        SessionController.PassStationMessage("MessageToAndroid,FoundHeadset");
                    }
                    enumerator.Dispose();
                    return current;
                }
            } while (enumerator.MoveNext());

            return currentViveStatus;
        }
    }
}
