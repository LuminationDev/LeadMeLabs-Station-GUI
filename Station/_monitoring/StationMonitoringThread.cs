using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Sentry;

namespace Station
{
    public class StationMonitoringThread
    {
        public static Thread? monitoringThread;
        public static DateTime latestHighTemperatureWarning = DateTime.Now;

        private static System.Timers.Timer? timer;
        private static Process[]? setVolErrors;
        private static bool restarting = false;

        /// <summary>
        /// Start a new thread with the Vive monitor check.
        /// </summary>
        public static void initializeMonitoring()
        {
            monitoringThread = new Thread(InitializeRespondingCheck);
            monitoringThread.Start();
        }

        public static void StopMonitoring()
        {
            monitoringThread?.Interrupt();
            timer?.Stop();
        }

        /// <summary>
        /// Start checking that VR applications and current Steam app are responding
        /// Will check every 5 seconds
        /// </summary>
        public static void InitializeRespondingCheck()
        {
            timer = new System.Timers.Timer(3000);
            timer.AutoReset = true;
            timer.Elapsed += callCheck;
            timer.Start();
        }

        private static int numberOfChecks = 0;
        /// <summary>
        /// Calls a function to check that all required VR processes are running
        /// If they are not sends a messages to the NUC/Tablet that there are tasks
        /// that aren't responding
        /// </summary>
        private static void callCheck(Object? source, System.Timers.ElapsedEventArgs e)
        {
            //Restart if the time equals xx::yy::zz
            if (TimeCheck(DateTime.Now.ToString("HH:mm:ss").Split(':')))
            {
                restarting = true; //do not double up on the command
                CommandLine.RestartProgram();
                return;
            }

            new Task(() => OpenVRCheck()).Start(); //Perform as separate task in case SteamVR is restarting.
            SetVolCheck();
            TemperatureCheck();

            Logger.WorkQueue();
        }

        /// <summary>
        /// Checks if OpenVR has been initialised, if not attempt to initialise it and query if there are any running
        /// application.
        /// </summary>
        private static void OpenVRCheck()
        {
            //Make sure that the vrmonitor (SteamVR) process is running
            if (Process.GetProcessesByName("vrmonitor").Length == 0) return;

            //Attempt to contact OpenVR, if this fails check the logs for errors
            if (Manager.openVRManager?.InitialiseOpenVR() ?? false)
            {
                Manager.openVRManager?.QueryCurrentApplication();
                Manager.openVRManager?.PerformDeviceChecks();
            } else
            {
                SteamScripts.CheckForSteamLogError();
            }
        }

        /// <summary>
        /// Retrieves a list of running processes with the name "SetVol" using `Process.GetProcessesByName`.
        /// Iterates through the list and, if a process has a non-empty main window title (which represents an error, 
        /// logs its termination and forcefully terminates the process using `process.Kill()`.
        /// </summary>
        private static void SetVolCheck()
        {
            setVolErrors = Process.GetProcessesByName("SetVol");
            for (int i = 0; i < setVolErrors.Length; i++)
            {
                Process process = setVolErrors[i];
                if (process.MainWindowTitle.Length > 0)
                {
                    Logger.WriteLog("Killing SetVol process: " + process.MainWindowTitle, MockConsole.LogLevel.Error);
                    process.Kill();
                }
            }
        }

        /// <summary>
        /// Performs a temperature check to monitor for high temperature conditions.
        /// Increments the count of temperature checks and evaluates conditions based on time and checks count.
        /// If enough time has passed and a certain number of checks have been performed, retrieves the current temperature.
        /// If the temperature exceeds 90 degrees, sends a response to the "Android" endpoint indicating "HighTemperature",
        /// logs the high temperature event, captures the event using Sentry for error tracking,
        /// and updates the timestamp for the latest high temperature warning.
        /// </summary>
        private static void TemperatureCheck()
        {
            numberOfChecks++;

            float? temperature = 0;
            if (DateTime.Now > latestHighTemperatureWarning.AddMinutes(5) && (numberOfChecks == 20))
            {
                numberOfChecks = 0;
                temperature = Temperature.GetTemperature();
            }

            if (temperature > 90)
            {
                Manager.SendResponse("Android", "Station", "HighTemperature");
                SentrySdk.CaptureMessage("High temperature detected (" + temperature + ") at: " +
                    (Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown"));
                Logger.WriteLog("High temperature detected (" + temperature + ") at: " +
                    (Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown"), MockConsole.LogLevel.Error);
                latestHighTemperatureWarning = DateTime.Now;
            }
        }

        /// <summary>
        /// Check if the time is within the window for restarting and that the program is not already restarting.
        /// </summary>
        /// <param name="time"></param>
        /// <returns>A boolean representing if the system should continue with restart</returns>
        private static bool TimeCheck(string[] time)
        {
            //Set the time when the program should restart
            string hour = "03"; //24-hour time
            string minute = "00";

            //the window between which the program can restart (10 seconds) should allow enough time to
            //capture a timer tick and not restart as soon as it opens again
            int[] window = { 0, 10 };

            return time[0].Equals(hour) && time[1].Equals(minute) && (Int32.Parse(time[2]) >= window[0] || Int32.Parse(time[2]) < window[1]) && !restarting;
        }
    }
}
