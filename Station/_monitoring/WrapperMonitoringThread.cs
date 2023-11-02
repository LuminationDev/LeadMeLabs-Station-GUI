using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Station
{
    public static class WrapperMonitoringThread
    {
        private static Thread? monitoringThread;
        private static bool steamError = false;
        private static System.Timers.Timer? timer;
        private static bool processesAreResponding = true;
        private static Process[]? allSteamProcesses;
        private static Process[]? vrMonitorProcesses;
        private static Process[]? allProcesses;
        private static Process[]? steamVrErrorDialogs;
        private static List<Process>? processes;

        /// <summary>
        /// An array representing the all process names needed to stop any VR session.
        /// </summary>
        public static readonly List<string> SteamProcesses = new() { "vrmonitor", "steam", "steamerrorreporter64" };
        public static readonly List<string> ViveProcesses = new() { "HtcConnectionUtility", "LhStatusMonitor", "WaveConsole", "ViveVRServer", "ViveSettings", "RRConsole", "RRServer" };

        /// <summary>
        /// Start a new thread with the supplied monitor check type.
        /// </summary>
        public static void InitializeMonitoring(string type)
        {
            monitoringThread = new Thread(() => {
                InitializeRespondingCheck(type);
            });

            monitoringThread.Start();
        }

        /// <summary>
        /// Stop the current monitor thread.
        /// </summary>
        public static void StopMonitoring()
        {
            monitoringThread?.Interrupt();
            timer?.Stop();
        }

        /// <summary>
        /// Start checking that VR applications and current Steam app are responding
        /// Will check every 5 seconds
        /// </summary>
        private static void InitializeRespondingCheck(string type)
        {
            timer = new System.Timers.Timer(3000);
            timer.AutoReset = true;

            switch (type)
            {
                case "Custom":
                    timer.Elapsed += CallCustomCheck;
                    break;

                case "Steam":
                    timer.Elapsed += CallSteamCheck;
                    break;

                case "Vive":
                    timer.Elapsed += CallViveCheck;
                    break;

                default:
                    Logger.WriteLog($"Monitoring type not supported: {type}", MockConsole.LogLevel.Error);
                    return;
            }

            timer.Start();
        }

        /// <summary>
        /// Calls a function to check that all required VR processes are running
        /// If they are not sends a messages to the Station application that there 
        /// are tasks that aren't responding.
        /// </summary>
        private static void CallCustomCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {
            SteamCheck();

            Logger.WorkQueue();
        }

        private static void CallSteamCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {
            MockConsole.WriteLine("Checked Vive status", MockConsole.LogLevel.Verbose);
            SteamCheck();

            Logger.WorkQueue();
        }

        private static void CallViveCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {


            Logger.WorkQueue();
        }

        /// <summary>
        /// Look for any steam errors, this may be from the Steam VR application or a Steam popup.
        /// </summary>
        private static void SteamCheck()
        {
            //Check the regular Steam processes are running
            List<string> combinedProcesses = new List<string>();
            combinedProcesses.AddRange(SteamProcesses);
            combinedProcesses.AddRange(ViveProcesses);

            processes = CommandLine.GetProcessesByName(combinedProcesses);
            bool processesAreAllResponding = CommandLine.CheckThatAllProcessesAreResponding(processes);
            bool allProcessesAreRunning = processes.Count >= SteamProcesses.Count;

            MockConsole.WriteLine($"Just checked that all processes are responding. Result: {processesAreAllResponding}", MockConsole.LogLevel.Verbose);
            MockConsole.WriteLine($"Just checked that all processes are running. Result: {allProcessesAreRunning}", MockConsole.LogLevel.Verbose);

            if (processesAreAllResponding != processesAreResponding)
            {
                processesAreResponding = processesAreAllResponding;
                if (!processesAreAllResponding)
                {
                    SessionController.PassStationMessage("MessageToAndroid,SetValue:state:Not Responding");
                }
                else
                {
                    SessionController.PassStationMessage("MessageToAndroid,SetValue:status:On");
                }
            }

            //Check for any Steam errors
            allSteamProcesses = Process.GetProcessesByName("steam");
            vrMonitorProcesses = Process.GetProcessesByName("vrmonitor");
            allProcesses = allSteamProcesses.Concat(vrMonitorProcesses).ToArray();
            for (int i = 0; i < allProcesses.Length; i++)
            {
                Process process = allProcesses[i];
                if (process.MainWindowTitle.Equals("Steam - Error") || process.MainWindowTitle.Equals("Unexpected SteamVR Error"))
                {
                    if (!steamError)
                    {
                        SessionController.PassStationMessage("MessageToAndroid,SteamError");
                    }

                    steamError = true;
                    break;
                }

                if (steamError && i == allProcesses.Length - 1)
                {
                    steamError = false;
                }
            }

            steamVrErrorDialogs = Process.GetProcessesByName("steamtours");
            foreach (var process in steamVrErrorDialogs)
            {
                Logger.WriteLog("Killing steam error process: " + process.MainWindowTitle, MockConsole.LogLevel.Error);
                process.Kill();
            }
            
            //Detect if a process contains the experience trying to be launched and the '- Steam' header which indicates a pop has occurred
            if (SteamWrapper.experienceName is null) return;
            
            Process[] searchProcesses = Process.GetProcesses();
            foreach (var process in searchProcesses)
            {
                if (!process.MainWindowTitle.Contains(SteamWrapper.experienceName)
                    || !process.MainWindowTitle.Contains("- Steam")
                    || SteamScripts.popupDetect) continue;
                
                //Only trigger once per experience
                SteamScripts.popupDetect = true;
                Console.WriteLine(process.MainWindowTitle);
                Manager.SendResponse("Android", "Station", "PopupDetected:" + SteamWrapper.experienceName);
            }
        }
    }
}
