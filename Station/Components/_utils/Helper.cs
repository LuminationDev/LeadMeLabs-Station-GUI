using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station.Components._enums;

namespace Station.Components._utils;

public static class Helper
{
    /// <summary>
    /// Hold a reference to the Station mode that has been set for this session.
    /// </summary>
    public static StationMode Mode { get; private set; }

    /// <summary>
    /// Collect and set the Mode for the session.
    /// </summary>
    public static void SetStationMode()
    {
        string? suppliedMode = Environment.GetEnvironmentVariable("StationMode", EnvironmentVariableTarget.Process);

        // Map station mode strings to their respective Mode enum values
        var modeMap = new Dictionary<string, StationMode>(StringComparer.OrdinalIgnoreCase)
        {
            { "vr", StationMode.VirtualReality },
            { "content", StationMode.Content },
            { "appliance", StationMode.Appliance },
            { "pod", StationMode.Pod }
        };

        if (suppliedMode == null || !modeMap.TryGetValue(suppliedMode, out var selectedMode))
        {
            Logger.WriteLog($"Station Mode is not set or unsupported: {suppliedMode ?? "null"}.", Enums.LogLevel.Error);
            throw new Exception("Station in unsupported mode");
        }

        // Set environment variable and update _mode
        Environment.SetEnvironmentVariable("StationMode", Attributes.GetEnumValue(selectedMode));
        Mode = selectedMode;
    }

    /// <summary>
    /// Check if the Station is VR compatible; this can be if the Mode is set to VR or Pod
    /// </summary>
    /// <returns>A bool if the Mode is VR or Pod</returns>
    public static bool IsStationVrCompatible()
    {
        return Mode is StationMode.VirtualReality or StationMode.Pod;
    }

    /// <summary>
    /// Check what the Mode of the Station is set to.
    /// </summary>
    /// <param name="modeToCheck"></param>
    /// <returns></returns>
    public static bool IsMode(StationMode modeToCheck)
    {
        return Mode == modeToCheck;
    }
    
    /// <summary>
    /// Get the lab location and station id of the Station. If they are null or undefined returns Unknown
    /// </summary>
    /// <returns>A string containing the lab location and station id.</returns>
    public static string GetLabLocationWithStationId()
    {
        return (Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Unknown") +
            (Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Unknown");
    }
    
    /// <summary>
    /// Monitors a specified condition using a loop that is delayed by 3 seconds each time the condition is not met, with
    /// optional timeout and attempt limits.
    /// </summary>
    /// <param name="conditionChecker">A delegate that returns a boolean value indicating whether the monitored condition is met.</param>
    /// <param name="attemptLimit">An int of the maximum amount of attempts the loop will wait for.</param>
    /// <returns>True if the condition was successfully met within the specified attempts; false otherwise.</returns>
    public static async Task<bool> MonitorLoop(Func<bool> conditionChecker, int attemptLimit)
    {
        //Track the attempts
        int monitorAttempts = 0;
        int delay = 3000;

        //Check the condition status (bail out after x amount)
        do
        {
            monitorAttempts++;
            await Task.Delay(delay);
        } while (conditionChecker.Invoke() && monitorAttempts < attemptLimit);

        return true;
    }
    
    /// <summary>
    /// Executes a task in the background (fire-and-forget) without awaiting its completion,
    /// while ensuring any exceptions that occur during task execution are logged.
    /// 
    /// This is useful for non-critical operations like logging or UI updates where task failures
    /// should not affect the main program flow, but exceptions still need to be observed and handled.
    /// </summary>
    /// <param name="task">The task to execute in the background.</param>
    public static void FireAndForget(Task task)
    {
        task.ContinueWith(t =>
        {
            // Log the exception if the task fails
            if (t.Exception != null)
            {
                Logger.WriteLog($"Exception in fire-and-forget task: {t.Exception}", Enums.LogLevel.Error);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
