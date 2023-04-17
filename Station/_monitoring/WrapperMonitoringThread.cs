using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Station
{
    public class WrapperMonitoringThread
    {
        public static Thread? monitoringThread;
        public static bool steamError = false;
        public static string viveStatus = "";

        private static System.Timers.Timer? timer;
        private static bool processesAreResponding = true;
        private static Process[]? allSteamProcesses;
        private static Process[]? vrMonitorProcesses;
        private static Process[]? allProcesses;
        private static Process[]? steamVrErrorDialogs;
        private static List<Process>? processes;

        /// <summary>
        /// An array representing the process names needed to stop a VR session.
        /// </summary>
        public static List<string> steamProcesses = new List<string> { "vrmonitor", "steam", "steamerrorreporter64" };
        public static List<string> viveProcesses = new List<string> { "HtcConnectionUtility", "LhStatusMonitor", "WaveConsole", "ViveVRServer", "ViveSettings" };

        /// <summary>
        /// Start a new thread with the supplied monitor check type.
        /// </summary>
        public static void initializeMonitoring(string type)
        {
            monitoringThread = new Thread(() => {
                initializeRespondingCheck(type);
            });

            monitoringThread.Start();
        }

        /// <summary>
        /// Stop the current monitor thread.
        /// </summary>
        public static void stopMonitoring()
        {
            monitoringThread?.Interrupt();
            timer?.Stop();
        }

        /// <summary>
        /// Start checking that VR applications and current Steam app are responding
        /// Will check every 5 seconds
        /// </summary>
        private static void initializeRespondingCheck(string type)
        {
            timer = new System.Timers.Timer(3000);
            timer.AutoReset = true;

            switch (type)
            {
                case "Custom":
                    timer.Elapsed += callCustomCheck;
                    break;

                case "Steam":
                    timer.Elapsed += callSteamCheck;
                    break;

                case "Vive":
                    timer.Elapsed += callViveCheck;
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
        private static void callCustomCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {
            ViveCheck();
            AppCheck();
            SteamCheck();

            Logger.WorkQueue();
        }

        private static void callSteamCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {
            MockConsole.WriteLine("About to check Vive status", MockConsole.LogLevel.Verbose);
            ViveCheck();
            MockConsole.WriteLine("Checked Vive status", MockConsole.LogLevel.Verbose);

            AppCheck();
            SteamCheck();

            Logger.WorkQueue();
        }

        private static void callViveCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {
            ViveCheck();
            AppCheck();

            Logger.WorkQueue();
        }

        /// <summary>
        /// Check that the currently running main process is responding. Only send once to avoid clogging up the 
        /// TCP server.
        /// </summary>
        private static void AppCheck()
        {
            bool? responding = WrapperManager.CurrentWrapper?.CheckCurrentProcess();
            if(responding == false)
            {
                SessionController.PassStationMessage("MessageToAndroid,SetValue:status:Not Responding");
            }
        }

        /// <summary>
        /// Look for any steam errors, this may be from the Steam VR application or a Steam popup.
        /// </summary>
        private static void SteamCheck()
        {
            //Check the regular Steam processes are running
            List<string> combinedProcesses = new List<string>();
            combinedProcesses.AddRange(steamProcesses);
            combinedProcesses.AddRange(viveProcesses);

            processes = CommandLine.getProcessesByName(combinedProcesses);
            bool processesAreAllResponding = CommandLine.checkThatAllProcessesAreResponding(processes);
            bool allProcessesAreRunning = processes.Count >= steamProcesses.Count;

            Console.WriteLine("Just checked that all processes are responding. Result: {0}", processesAreAllResponding);
            Console.WriteLine("Just checked that all processes are running. Result: {0}", allProcessesAreRunning);

            if (processesAreAllResponding != processesAreResponding)
            {
                processesAreResponding = processesAreAllResponding;
                if (!processesAreAllResponding)
                {
                    SessionController.PassStationMessage("MessageToAndroid,SetValue:status:Not Responding");
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
            for (int i = 0; i < steamVrErrorDialogs.Length; i++)
            {
                Process process = steamVrErrorDialogs[i];
                Logger.WriteLog("Killing steam error process: " + process.MainWindowTitle, MockConsole.LogLevel.Error);
                SentrySdk.CaptureMessage("Killing steam error process: " + process.MainWindowTitle);
                process.Kill();
            }


            //Detect if a process contains the experience trying to be launched and the '- Steam' header which indicates a pop has occurred
            if (SteamWrapper.experienceName is not null)
            {
                Process[] searchProcesses = Process.GetProcesses();
                for (int i = 0; i < searchProcesses.Length; i++)
                {
                    Process process = searchProcesses[i];

                    if (process.MainWindowTitle.Contains(SteamWrapper.experienceName)
                        && process.MainWindowTitle.Contains("- Steam")
                        && !SteamScripts.popupDetect)
                    {
                        //Only trigger once per experience
                        SteamScripts.popupDetect = true;
                        Console.WriteLine(process.MainWindowTitle);
                        Manager.sendResponse("Android", "Station", "PopupDetected:" + SteamWrapper.experienceName);
                    }
                }
            }
        }

        private static void ViveCheck()
        {
            if (SessionController.vrHeadset == null) return;

            viveStatus = SessionController.vrHeadset.MonitorVrConnection(currentViveStatus: viveStatus);
            Logger.WriteLog("ViveStatus: " + viveStatus, MockConsole.LogLevel.Debug);
        }
    }
}
