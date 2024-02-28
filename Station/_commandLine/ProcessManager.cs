using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Station._notification;
using Station._utils;

namespace Station._commandLine;

/// <summary>
/// Provides methods to manage and retrieve information about running processes on the system. Ensures thread safety
/// and real-time updates of the process list.
/// </summary>
public static class ProcessManager
{
    private static List<Process> runningProcesses = new();
    private static readonly object LockObject = new();
    private static DateTime lastUpdate = DateTime.MinValue;

    /// <summary>
    /// Retrieves the list of currently running processes. Ensures thread safety by using a lock to manage access to the
    /// shared process list. If the lock is already taken by another thread, returns the current list. If the stored
    /// process list is more than 1 second old, updates the list by fetching the current processes and returns the updated list.
    /// </summary>
    /// <returns>The list of currently running processes.</returns>
    public static IEnumerable<Process> GetRunningProcesses()
    {
        var lockTaken = false;
        try
        {
            Monitor.TryEnter(LockObject, ref lockTaken);
            if (!lockTaken)
            {
                // If lock is taken by another thread, return the current list.
                return runningProcesses;
            }

            if (!((DateTime.Now - lastUpdate).TotalSeconds > 1))
            {
                // If list is less than 1 second old, return the current list.
                return runningProcesses;
            }
            
            runningProcesses = new List<Process>(Process.GetProcesses());
            lastUpdate = DateTime.Now;

            return runningProcesses;
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(LockObject);
            }
        }
    }

    /// <summary>
    /// Retrieves a list of processes that match the provided list of process titles.
    /// Uses the GetProcessesByName method for each title in the list, collecting and returning a combined list of processes matching any title in the provided list.
    /// </summary>
    /// <param name="processTitles">A list of process titles to search for.</param>
    /// <returns>A list of processes that match any of the provided process titles.</returns>
    public static List<Process> GetProcessesByNames(List<string>? processTitles)
    {
        return (processTitles ?? new List<string>()).SelectMany(GetProcessesByName).ToList();
    }

    /// <summary>
    /// Retrieves all processes matching the provided name from the current list of running processes. Uses the
    /// GetRunningProcesses() method to obtain the list of processes, then filters and returns a new list containing
    /// processes that match the given name.
    /// </summary>
    /// <param name="name">The name of the process to search for.</param>
    /// <returns>A list of processes matching the provided name.</returns>
    public static Process[] GetProcessesByName(string? name)
    {
        var processes = GetRunningProcesses();
        return processes.Where(p => p.ProcessName == name).ToArray();
    }

    /// <summary>
    /// Retrieves a process by its unique identifier (ID) from the current list of running processes. Uses the
    /// GetRunningProcesses() method to obtain the list of processes, then retrieves the first process that matches
    /// the provided ID.
    /// </summary>
    /// <param name="id">The ID of the process to retrieve.</param>
    /// <returns>The process with the provided ID, if found; otherwise, returns null.</returns>
    public static Process? GetProcessById(int? id)
    {
        var processes = GetRunningProcesses();
        return processes.FirstOrDefault(p => p.Id == id);
    }
    
    /// <summary>
    /// Retrieves the main window titles associated with the specified process name.
    /// </summary>
    /// <param name="processName">The name of the process to retrieve window titles for.</param>
    /// <returns>
    /// A list of main window titles associated with the specified process name.
    /// If the process is not running or has no visible main window titles, returns null.
    /// </returns>
    public static List<string> GetProcessMainWindowTitle(string processName)
    {
        List<string> windowTitles = new List<string>();

        Process[] processes = GetProcessesByName(processName);
        foreach (Process process in processes)
        {
            if (!string.IsNullOrEmpty(process.MainWindowTitle))
            {
                windowTitles.Add(process.MainWindowTitle);
            }
        }

        return windowTitles;
    }
    
    /// <summary>
    /// Provide a list of process names and ids and get a boolean if they are responding
    /// If the process names do not have a running process, they will NOT return false
    /// </summary>
    /// <param name="allProcesses">Array of process names to check</param>
    /// <returns>boolean of if any of the processes are not responding according to powershell</returns>
    public static bool CheckThatAllProcessesAreResponding(List<Process> allProcesses)
    {
        foreach (var process in allProcesses.Where(process => !process.Responding))
        {
            Logger.WriteLog($"Process is not responding: {process.ProcessName}", MockConsole.LogLevel.Normal);
            return false;
        }

        Logger.WriteLog("All processes are responding", MockConsole.LogLevel.Verbose, false);
        return true;
    }
}
