using System;
using System.Diagnostics;
using System.Threading;
using Sentry;

namespace Station
{
    public class StationMonitoringThread
    {
        public static Thread? monitoringThread;
        private static System.Timers.Timer? timer;
        private static Process[]? setVolErrors;
        private static bool restarting = false;
        public static DateTime latestHighTemperatureWarning = DateTime.Now;

        /// <summary>
        /// Start a new thread with the Vive monitor check.
        /// </summary>
        public static void initializeMonitoring()
        {
            monitoringThread = new Thread(initializeRespondingCheck);
            monitoringThread.Start();
        }

        public static void stopMonitoring()
        {
            monitoringThread?.Interrupt();
            timer?.Stop();
        }

        /// <summary>
        /// Start checking that VR applications and current Steam app are responding
        /// Will check every 5 seconds
        /// </summary>
        public static void initializeRespondingCheck()
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
            numberOfChecks++;
            //Restart if the time equals xx::yy::zz
            if (timeCheck(DateTime.Now.ToString("HH:mm:ss").Split(':')))
            {
                restarting = true; //do not double up on the command
                CommandLine.RestartProgram();
                return;
            }

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

            float? temperature = 0;
            if (DateTime.Now > latestHighTemperatureWarning.AddMinutes(5) && (numberOfChecks == 20))
            {
                numberOfChecks = 0;
                temperature = Temperature.GetTemperature();
            }

            if (temperature > 90)
            {
                Manager.SendResponse("Android", "Station", "HighTemperature");
                SentrySdk.CaptureMessage("High temperature detected (" + temperature + ") at: " + (Environment.GetEnvironmentVariable("LabLocation") ?? "Unknown"));
                Logger.WriteLog("High temperature detected (" + temperature + ") at: " + (Environment.GetEnvironmentVariable("LabLocation") ?? "Unknown"), MockConsole.LogLevel.Error);
                latestHighTemperatureWarning = DateTime.Now;
            }

            Logger.WorkQueue();
        }

        /// <summary>
        /// Check if the time is within the window for restarting and that the program is not already restarting.
        /// </summary>
        /// <param name="time"></param>
        /// <returns>A boolean representing if the system should continue with restart</returns>
        private static bool timeCheck(string[] time)
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
