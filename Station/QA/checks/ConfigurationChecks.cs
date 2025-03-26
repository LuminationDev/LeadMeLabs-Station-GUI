using System;
using System.Collections.Generic;
using System.IO;
using Station.Components._commandLine;
using Station.Components._utils;

namespace Station.QA.checks;

public class ConfigurationChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa(string labType)
    {
        _qaChecks = new List<QaCheck>
        {
            IsTaskSchedulerCreated(),
            IsOldTaskSchedulerNotPresent(),
            IsShellStartupNotPresent()
        };
        _qaChecks.AddRange(CheckEnvironmentVariables());

        return _qaChecks;
    }
    
    /// <summary>
    /// Query the local computers Scheduled tasks looking for the Software_Checker, return it display name and the status
    /// of Enabled or Disabled. If there is no task return Not found.
    /// </summary>
    private QaCheck IsTaskSchedulerCreated()
    {
        QaCheck qaCheck = new QaCheck("task_scheduler_created");
        const string taskFolder = "LeadMe\\Software_Checker";
        const string command = $"SCHTASKS /QUERY /TN \"{taskFolder}\" /fo LIST";

        string? stdout = StationCommandLine.RunProgramWithOutput("cmd.exe", $"/C {command}");
        if (string.IsNullOrWhiteSpace(stdout))
        {
            qaCheck.SetFailed("Could not find LeadMe\\Software_Checker");
            return qaCheck;
        }

        string[] lines = stdout.Split('\n');
        foreach (string line in lines)
        {
            if (line.Contains("TaskName:"))
            {
                if (!line.Contains("LeadMe\\Software_Checker"))
                {
                    qaCheck.SetFailed("Task is not named: LeadMe\\Software_Checker. Name is: " + line.Replace("TaskName:", "").Trim());
                    return qaCheck;
                }
            }
            else if (line.Contains("Status:"))
            {
                if (line.Contains("Disabled"))
                {
                    qaCheck.SetFailed("LeadMe\\Software_Checker is disabled");
                    return qaCheck;
                }
            }
        }
        qaCheck.SetPassed(null);
        return qaCheck;
    }
    
    /// <summary>
    /// Query the local computers Scheduled tasks looking for the Software_Checker, return it display name and the status
    /// of Enabled or Disabled. If there is no task return Not found.
    /// </summary>
    private QaCheck IsOldTaskSchedulerNotPresent()
    {
        QaCheck qaCheck = new QaCheck("old_task_scheduler_not_existing");
        const string taskFolder = "Station\\Station_Checker";
        const string command = $"SCHTASKS /QUERY /TN \"{taskFolder}\" /fo LIST";

        string? stdout = StationCommandLine.RunProgramWithOutput("cmd.exe", $"/C {command}");
        if (string.IsNullOrWhiteSpace(stdout))
        {
            qaCheck.SetPassed("Could not find Station\\Station_Checker");
            return qaCheck;
        }

        if (stdout.Contains("ERROR: The system cannot find the file specified."))
        {
            qaCheck.SetPassed("Could not find Station\\Station_Checker");
            return qaCheck;
        }

        string[] lines = stdout.Split('\n');
        foreach (string line in lines)
        {
            if (line.Contains("Status:"))
            {
                if (!line.Contains("Disabled"))
                {
                    qaCheck.SetFailed("Station\\Station_Checker is present and enabled");
                    return qaCheck;
                }
            }
        }
        qaCheck.SetPassed(null);
        return qaCheck;
    }
    
    /// <summary>
    /// Check if shortcut to NUC is not present in shell:startup
    /// </summary>
    private QaCheck IsShellStartupNotPresent()
    {
        QaCheck qaCheck = new QaCheck("shell_startup_not_existing");
        String filePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\Station.exe.lnk";
        bool shellStartupExists = File.Exists(filePath);

        if (shellStartupExists)
        {
            qaCheck.SetFailed(filePath);
            return qaCheck;
        }

        qaCheck.SetPassed(null);
        return qaCheck;
    }

    private List<QaCheck> CheckEnvironmentVariables()
    {
        List<QaCheck> list = new List<QaCheck>();
        list.Add(CheckEnvironmentVariable("LabLocation", "environment_lab_location"));
        list.Add(CheckEnvironmentVariable("SteamUserName", "environment_steam_username"));
        list.Add(CheckEnvironmentVariable("SteamPassword", "environment_steam_password"));
        list.Add(CheckEnvironmentVariable("NucAddress", "environment_nuc_address"));
        list.Add(CheckEnvironmentVariable("StationId", "environment_station_id"));
        list.Add(CheckEnvironmentVariable("AppKey", "environment_encryption_key"));
        if (Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            list.Add(CheckEnvironmentVariable("HeadsetType", "environment_headset_type"));
        }
        else
        {
            QaCheck qaCheck = new QaCheck("environment_headset_type");
            qaCheck.SetPassed("Station is a non-vr station");
            list.Add(qaCheck);
        }
        list.Add(CheckEnvironmentVariable("room", "environment_room"));
        list.Add(CheckEnvironmentVariable("StationMode", "environment_station_mode"));
        return list;
    }

    private QaCheck CheckEnvironmentVariable(string environmentVariableKey, string checkName)
    {
        QaCheck qaCheck = new QaCheck(checkName);
        bool existsInProcess =
            (Environment.GetEnvironmentVariable(environmentVariableKey, EnvironmentVariableTarget.Process) != null);
        bool existsInUser =
            (Environment.GetEnvironmentVariable(environmentVariableKey, EnvironmentVariableTarget.User) != null);
        if (existsInUser)
        {
            qaCheck.SetFailed("Found variable in user environment variables");
            return qaCheck;
        }

        if (!existsInProcess)
        {
            qaCheck.SetFailed("Could not find config variable, please check that it is set with the launcher");
            return qaCheck;
        }
        
        qaCheck.SetPassed(null);
        return qaCheck;
    }
}