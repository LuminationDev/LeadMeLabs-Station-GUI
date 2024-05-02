using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.MVC.Controller;

namespace Station.Components._utils;

public static class DeviceControl
{
    private static bool restarting;
    private static bool isUpdating;
    
    /// <summary>
    /// Track if the Station is updating as to not turn off the Station while it is updating.
    /// </summary>
    /// <returns></returns>
    public static bool GetIsUpdating()
    {
        return isUpdating;
    }
    
    /// <summary>
    /// Process name of the Launcher that coordinates the LeadMe software suite.
    /// </summary>
    private const string LauncherProcessName = "LeadMe";

    /// <summary>
    /// Check if any actions are required to be undertaken at the current time.
    /// This includes restarting the Station at 3:00am, updating Steam applications on Tuesday's or Wednesdays and updating
    /// Windows on the third Thursday of the month.
    /// Return true if the NUC is restarting as to not double up.
    /// </summary>
    /// <returns>A bool of if the program is restarting.</returns>
    public static bool CheckForTimedActions()
    {
        string[] time = DateTime.Now.ToString("HH:mm:ss").Split(':');
        string currentHour = time[0];

        // Early exit if not in the specified hour
        if (currentHour != "03")
        {
            return false;
        }
        
        //Set the time when the program should restart
        const string restartHour = "03"; //24-hour time
        const string restartMinute = "00";
        
        // Check for restart program
        if (TimeCheck(time, restartHour, restartMinute))
        {
            restarting = true; //do not double up on the command
            RestartProgram();
            return true;
        }

        //TODO Disabled until there is an update to fully test on
        return false;
        
        // Only perform Windows updates on the third Thursday of the month
        if (IsThirdThursdayOfMonth())
        {
            new Thread(() => PerformWindowsUpdates(time)).Start();
        }
        
        // Only perform overnight updates on Tuesdays and Wednesdays (Steam)
        if (IsWednesdayOrTuesday()) return false;
        
        PerformOvernightUpdates(time);
        return false;
    }
    
    /// <summary>
    /// Attempt to check and perform any pending Windows updates.
    /// </summary>
    /// <param name="time">A string array, containing the current hour and current minute to check against.</param>
    private static void PerformWindowsUpdates(string[] time)
    {
        const string updateHour = "03"; //24-hour time
        const string updateMinute = "45";

        if (isUpdating || !TimeCheck(time, updateHour, updateMinute)) return;
        
        JObject stateMessage = new JObject
        {
            { "action", "SoftwareState" },
            { "value", "Updating..." }
        };
        ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(stateMessage), TimeSpan.FromSeconds(1));
        
        isUpdating = true;
        WindowsUpdates.Update(Logger.WriteLog);
        isUpdating = false;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="time">A string array, containing the current hour and current minute to check against.</param>
    private static void PerformOvernightUpdates(string[] time)
    {
        //TODO look into automating Steam updates
        // //Set the time when the stations should attempt updates
        // const string updateHour = "03"; //24-hour time
        // const string updateMinute = "45";
        //
        // // Check if the time is right for steam updates
        // if (!_isUpdating && IsWednesdayOrTuesday() && TimeCheck(time, updateHour, updateMinute))
        // {
        //     _isUpdating = true;
        //     //Perform Steam updates??
        //     _isUpdating = false;
        // }
    }
    
    /// <summary>
    /// Kill off the launcher program if the time is between a set amount. The Software_Checker scheduler task will automatically restart the
    /// application within the next five minutes, updating the Launcher and Station software.
    /// </summary>
    private static void RestartProgram()
    {
        Logger.WriteLog("Daily restart", Enums.LogLevel.Normal);

        List<Process> processes = ProcessManager.GetProcessesByNames(new List<string>{ LauncherProcessName });
        
        foreach (Process process in processes)
        {
            try
            {
                process.Kill(true);
            }
            catch (Exception e)
            {
                Logger.WriteLog($"Error: {e}", Enums.LogLevel.Normal);
            }
        }
        
        // Exit the application
        Environment.Exit(0);
    }
    
    /// <summary>
    /// Check the current day, if it is a Tuesday or Wednesday perform the overnight updates.
    /// This is performed on a Tuesday and Wednesday as a LeadMe releases on a Monday and patches are released on Tuesdays.
    /// </summary>
    /// <returns>A bool, true if it is a Tuesday or Wednesday, false if any other day.</returns>
    private static bool IsWednesdayOrTuesday()
    {
        // Get the current date and time
        DateTime currentDate = DateTime.Now;

        // Check if the current day of the week is a Tuesday or Wednesday
        return currentDate.DayOfWeek is DayOfWeek.Wednesday or DayOfWeek.Tuesday;
    }
    
    /// <summary>
    /// Check to see if the current day is the third Thursday of the month (Roughly 1 week after microsoft patches
    /// come out).
    /// </summary>
    /// <returns>A bool, true if it is the third Thursday of the moth, false if any other day.</returns>
    private static bool IsThirdThursdayOfMonth()
    {
        // Get the current date and time
        DateTime currentDate = DateTime.Now;
        
        // Check if the date is a Thursday and falls between the 15th and 21st of the month
        //return currentDate is { DayOfWeek: DayOfWeek.Thursday, Day: >= 15 and <= 21 };
        return currentDate.DayOfWeek is DayOfWeek.Thursday;
    }
    
    /// <summary>
    /// Check if the time is within the window for restarting and that the program is not already restarting.
    /// </summary>
    /// <param name="time">A string list containing the current hour [0], minute [1] and seconds [2].</param>
    /// <param name="expectedHour">A string of the hour to check for.</param>
    /// <param name="expectedMinute">A string of the minute to check for.</param>
    /// <returns>A boolean representing if the system should continue with restart</returns>
    private static bool TimeCheck(string[] time, string expectedHour, string expectedMinute)
    {
        //the window between which the program can restart (7 seconds) should allow enough time to
        //capture a timer tick and not restart as soon as it opens again
        int[] window = { 0, 7 };

        return time[0].Equals(expectedHour) && time[1].Equals(expectedMinute) && (Int32.Parse(time[2]) >= window[0] && Int32.Parse(time[2]) < window[1]) && !restarting;
    }
}
