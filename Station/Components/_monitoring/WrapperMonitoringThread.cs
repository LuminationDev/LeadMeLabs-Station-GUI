using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Station.Components._commandLine;
using Station.Components._notification;
using Station.Components._utils;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;

namespace Station.Components._monitoring;

public static class WrapperMonitoringThread
{
    private static Thread? monitoringThread;
    private static bool steamError;
    private static System.Timers.Timer? timer;
    private static bool processesAreResponding = true;

    /// <summary>
    /// An array representing the all process names needed to stop any VR session.
    /// </summary>
    public static readonly List<string> SteamProcesses = new() { "vrmonitor", "steam", "steamerrorreporter64" };
    public static readonly List<string> ViveProcesses = new() { "HtcConnectionUtility", "LhStatusMonitor", "WaveConsole", "ViveVRServer", "ViveSettings", "RRConsole", "RRServer" };
    public static readonly List<string> ReviveProcesses = new() { "ReviveOverlay" }; 
    
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
            
            case "Revive":
                timer.Elapsed += CallReviveCheck;
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
        ProcessesAreResponding();
        SteamCheck();

        Logger.WorkQueue();
    }

    private static void CallSteamCheck(Object? source, System.Timers.ElapsedEventArgs e)
    {
        MockConsole.WriteLine("Checked Steam status", MockConsole.LogLevel.Verbose);
        ProcessesAreResponding();
        SteamCheck();

        Logger.WorkQueue();
    }
    
    private static void CallReviveCheck(Object? source, System.Timers.ElapsedEventArgs e)
    {
        MockConsole.WriteLine("Checked Revive status", MockConsole.LogLevel.Verbose);
        ProcessesAreResponding();
        SteamCheck();

        Logger.WorkQueue();
    }

    private static void CallViveCheck(Object? source, System.Timers.ElapsedEventArgs e)
    {
        Logger.WorkQueue();
    }

    /// <summary>
    /// Check that the necessary processes are responding for the current headset/application
    /// </summary>
    private static void ProcessesAreResponding()
    {
        List<string> combinedProcesses = new List<string>();
        combinedProcesses.AddRange(SteamProcesses);
        combinedProcesses.AddRange(ViveProcesses);
        combinedProcesses.AddRange(ReviveProcesses);

        //Check the regular Steam processes are running
        List<Process> runningSteamProcesses = ProcessManager.GetProcessesByNames(SteamProcesses);
        bool allProcessesAreRunning = runningSteamProcesses.Count >= SteamProcesses.Count;
        
        //Check all processes are responding
        List<Process> processes = ProcessManager.GetProcessesByNames(combinedProcesses);
        bool processesAreAllResponding = ProcessManager.CheckThatAllProcessesAreResponding(processes);
        
        MockConsole.WriteLine($"Just checked that all processes are responding. Result: {processesAreAllResponding}", MockConsole.LogLevel.Verbose);
        MockConsole.WriteLine($"Just checked that all processes are running. Result: {allProcessesAreRunning}", MockConsole.LogLevel.Verbose);

        if (processesAreAllResponding == processesAreResponding) return;
        
        processesAreResponding = processesAreAllResponding;
        
        SessionController.PassStationMessage(!processesAreAllResponding
            ? "MessageToAndroid,SetValue:state:Not Responding"
            : "MessageToAndroid,SetValue:status:On");
    }

    /// <summary>
    /// Look for any steam errors, this may be from the Steam VR application or a Steam popup.
    /// </summary>
    private static void SteamCheck()
    {
        //Check for any Steam errors
        List<string> errorTitles = new List<string> { "Steam - Error", "Unexpected SteamVR Error" };
        bool hasSteamError =  ProcessManager.GetRunningProcesses().Any(process => errorTitles.Contains(process.MainWindowTitle));

        if (hasSteamError && !steamError)
        {
            SessionController.PassStationMessage("MessageToAndroid,SteamError");
            steamError = true;
        }
        else if (!hasSteamError && steamError)
        {
            steamError = false;
        }

        Process[] steamVrErrorDialogs = ProcessManager.GetProcessesByName("steamtours");
        foreach (var process in steamVrErrorDialogs)
        {
            Logger.WriteLog($"Killing steam error process: {process.MainWindowTitle}", MockConsole.LogLevel.Error);
            process.Kill();
        }
        
        //Detect if a process contains the experience trying to be launched and the '- Steam' header which indicates a pop has occurred
        if (SteamWrapper.experienceName is null) return;
        
        IEnumerable<Process> searchProcesses = ProcessManager.GetRunningProcesses();
        
        var targetProcess = searchProcesses.FirstOrDefault(process =>
            process.MainWindowTitle.Contains(SteamWrapper.experienceName) &&
            process.MainWindowTitle.Contains("- Steam") &&
            !SteamScripts.popupDetect
        );

        if (targetProcess == null) return;
        
        //Only trigger once per experience
        SteamScripts.popupDetect = true;
        MessageController.SendResponse("Android", "Station", $"PopupDetected:{SteamWrapper.experienceName}");
    }
}
