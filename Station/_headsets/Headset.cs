using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace Station._headsets
{
    /// <summary>
    /// A class to hold generic functions that all headset classes use.
    /// </summary>
    public class Headset
    {
        private Timer? _timer;
        private static bool minimising = false;

        /// <summary>
        /// Initiates a process minimization routine for a collection of VR-related processes.
        /// The method sets a timer to attempt minimizing the specified VR processes at regular intervals.
        /// After 30 seconds (or 6 attempts at 5-second intervals), the minimization process stops.
        /// </summary>
        /// <param name="vrProcesses">Collection of strings representing VR-related processes to be minimized.</param>
        protected void Minimize(IEnumerable<string> vrProcesses)
        {
            if (minimising) return;
            
            minimising = true;
            _timer = new Timer(5000); // every 5 seconds try to minimize the processes
            int attempts = 0;

            _timer.Elapsed += TimerElapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            return;

            void TimerElapsed(object? obj, ElapsedEventArgs args)
            {
                MinimizeVrProcesses(vrProcesses);
                attempts++;
                if (attempts <= 6) return; // after 30 seconds, we can stop
                _timer.Stop();
                minimising = false;
            }
        }

        /// <summary>
        /// Minimizes the specified VR-related processes by iterating through the collection of process names.
        /// For each identified process, a log entry is written, indicating the initiation of the minimization process,
        /// and the associated process is minimized by utilizing the WindowManager class.
        /// </summary>
        /// <param name="vrProcesses">Collection of strings representing VR-related processes to be minimized.</param>
        private void MinimizeVrProcesses(IEnumerable<string> vrProcesses)
        {
            Logger.WriteLog("minimizing processes", MockConsole.LogLevel.Verbose);
            foreach (var process in vrProcesses.Select(Process.GetProcessesByName).SelectMany(processes => processes))
            {
                Logger.WriteLog("minimizing: " + process.ProcessName, MockConsole.LogLevel.Verbose);
                WindowManager.MinimizeProcess(process);
            }
        }

        /// <summary>
        /// Checks if all specified VR-related processes are currently running by comparing their names with the active processes.
        /// It retrieves all currently running processes and compares their names against the provided list of VR-related processes.
        /// Processes that match the provided list are stored in a HashSet.
        /// The method returns a boolean indicating whether all specified VR-related processes are currently running.
        /// </summary>
        /// <param name="vrProcesses">A list of strings representing VR-related processes to check for their presence.</param>
        /// <returns>True if all specified VR-related processes are found among the currently running processes; otherwise, false.</returns>
        protected bool QueryMonitorProcesses(List<string> vrProcesses)
        {
            HashSet<string> list = new();
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                if (vrProcesses.Contains(process.ProcessName))
                {
                    list.Add(process.ProcessName);
                }
            }

            return list.Count == vrProcesses.Count;
        }
    }
}
