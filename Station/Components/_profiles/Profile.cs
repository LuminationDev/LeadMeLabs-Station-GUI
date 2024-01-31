using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;

namespace Station.Components._profiles;

/// <summary>
/// A class to hold generic functions that all profile classes use.
/// </summary>
public class Profile
{
    private Timer? _timer;
    private static bool minimising;

    /// <summary>
    /// Initiates a process minimization routine for a collection of related processes.
    /// The method sets a timer to attempt minimizing the specified VR processes at regular intervals.
    /// After 30 seconds (or 6 attempts at 5-second intervals), the minimization process stops.
    /// </summary>
    /// <param name="processes">Collection of strings representing related processes to be minimized.</param>
    /// <param name="attemptLimit">An int of how many times the minimise process should run for.</param>
    protected void Minimize(IEnumerable<string> processes, int attemptLimit)
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
            MinimizeProcesses(processes);
            attempts++;
            if (attempts <= attemptLimit) return; // after 30 seconds, we can stop
            _timer.Stop();
            minimising = false;
        }
    }

    /// <summary>
    /// Minimizes the specified processes by iterating through the collection of process names.
    /// For each identified process, a log entry is written, indicating the initiation of the minimization process,
    /// and the associated process is minimized by utilizing the WindowManager class.
    /// </summary>
    /// <param name="processes">Collection of strings representing processes to be minimized.</param>
    private void MinimizeProcesses(IEnumerable<string> processes)
    {
        if (!InternalDebugger.GetMinimisePrograms()) return;
        
        Logger.WriteLog("minimizing processes", MockConsole.LogLevel.Verbose);
        foreach (var process in processes.Select(ProcessManager.GetProcessesByName).SelectMany(selectedProcesses => selectedProcesses))
        {
            Logger.WriteLog($"minimizing: {process.ProcessName}", MockConsole.LogLevel.Verbose);
            WindowManager.MinimizeProcess(process);
        }
    }

    /// <summary>
    /// Checks if all specified processes are currently running by comparing their names with the active processes.
    /// It retrieves all currently running processes and compares their names against the provided list of VR-related processes.
    /// Processes that match the provided list are stored in a HashSet.
    /// The method returns a boolean indicating whether all specified processes are currently running.
    /// </summary>
    /// <param name="processes">A list of strings representing processes to check for their presence.</param>
    /// <returns>True if all specified processes are found among the currently running processes; otherwise, false.</returns>
    protected bool QueryMonitorProcesses(List<string> processes)
    {
        HashSet<string> list = new();
        IEnumerable<Process> runningProcesses = ProcessManager.GetRunningProcesses();

        foreach (Process process in runningProcesses)
        {
            if (processes.Contains(process.ProcessName))
            {
                list.Add(process.ProcessName);
            }
        }

        return list.Count == processes.Count;
    }
    
    /// <summary>
    /// Attempts to cast the specified object to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to which the object is casted.</typeparam>
    /// <param name="obj">The object to cast.</param>
    /// <returns>
    /// If the casting is successful, returns the casted object;
    /// otherwise, returns null.
    /// </returns>
    public static T? CastToType<T>(object? obj) where T : class
    {
        if (obj is T castedObject)
        {
            return castedObject;
        }

        return null;
    }
}
