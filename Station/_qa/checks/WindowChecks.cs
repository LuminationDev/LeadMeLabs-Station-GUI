﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.Http;
using Microsoft.Win32;
using Newtonsoft.Json;
using LeadMeLabsLibrary;

namespace Station._qa.checks;

public class WindowChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa()
    {
        _qaChecks.Add(IsWakeOnMagicPacketEnabled());
        _qaChecks.Add(IsAmdInstalled());
        _qaChecks.Add(CheckEnvAsync());
        _qaChecks.Add(CheckWallpaper());
        _qaChecks.Add(CheckTimezone());
        _qaChecks.Add(CheckTimeAndDate());
        _qaChecks.Add(IsTaskSchedulerCreated());
        _qaChecks.Add(IsOldTaskSchedulerNotPresent());
        _qaChecks.Add(IsDriverEasyNotInstalled());
        _qaChecks.Add(IsNvidiaNotInstalled());
        _qaChecks.Add(IsAllowedThroughFirewall());
        _qaChecks.Add(IsLauncherAllowedThroughFirewall());
        _qaChecks.Add(CanAccessStationHeroku());
        _qaChecks.Add(CanAccessLauncherHeroku());

        return _qaChecks;
    }
    
    /// <summary>
    /// Is program allowed through firewall
    /// </summary>
    private QaCheck IsAllowedThroughFirewall()
    {
        QaCheck qaCheck = new QaCheck("allowed_through_firewall");

        string result = FirewallManagement.IsProgramAllowedThroughFirewall() ?? "Unknown";
        if (result.Equals("Allowed"))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("Program not allowed through firewall");
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Is launcher allowed through firewall
    /// </summary>
    private QaCheck IsLauncherAllowedThroughFirewall()
    {
        QaCheck qaCheck = new QaCheck("allowed_through_firewall");

        string result = FirewallManagement.IsProgramAllowedThroughFirewall($"C:\\Users\\{Environment.GetEnvironmentVariable("UserDirectory")}\\AppData\\Local\\Programs\\LeadMe") ?? "Unknown";
        if (result.Equals("Allowed"))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("Program not allowed through firewall");
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Query the main network adapter (this should only be one, however test this)
    /// </summary>
    private QaCheck IsWakeOnMagicPacketEnabled()
    {
        QaCheck qaCheck = new QaCheck("magic_packet_enabled");
        const string powershellCommand = "Get-NetAdapterAdvancedProperty -Name '*' -RegistryKeyword '*WakeOnMagicPacket' | Select-Object -Property Name, DisplayName, DisplayValue";

        string? output = CommandLine.RunProgramWithOutput("powershell.exe", $"-NoProfile -ExecutionPolicy unrestricted -Command \"{powershellCommand}\"");

        if (output == null)
        {
            qaCheck.SetFailed("Couldn't find any value for wake on magic packet");
            return qaCheck;
        }
        
        string[] lines = output.Split('\n');
        foreach (string line in lines)
        {
            if (line.Contains("Wake on Magic Packet"))
            {
                string[] split = line.Split("Wake on Magic Packet");
                if (split[1].Contains("Enabled"))
                {
                    qaCheck.SetPassed(null);
                }
                else
                {
                    qaCheck.SetFailed("Value for wake on magic packet was not enabled. Value: " + split[1]);
                }

                return qaCheck;
            }
        }
        
        qaCheck.SetFailed("Couldn't find any value for wake on magic packet");
        return qaCheck;
    }
    
    /// <summary>
    /// Checks if AMD Adrenalin is installed on the system.
    /// </summary>
    private QaCheck IsAmdInstalled()
    {
        QaCheck qaCheck = new QaCheck("amd_installed");
        const string adrenalinSearchKey = @"SOFTWARE\AMD";
        const string adrenalinValueName = "DisplayName";
        const string adrenalinValueExpected = "AMD Radeon Software";

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(adrenalinSearchKey))
            {
                if (key != null)
                {
                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey != null)
                            {
                                object value = subKey.GetValue(adrenalinValueName);
                                if (value != null && value.ToString() == adrenalinValueExpected)
                                {
                                    qaCheck.SetPassed(null);
                                    return qaCheck;
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("AMD Adrenalin is not installed.");
            qaCheck.SetFailed("AMD Adrenalin is not installed.");
            return qaCheck;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            qaCheck.SetFailed($"Error: {ex.Message}");
            return qaCheck;
        }
    }
    
    /// <summary>
    /// Checks that DriverEasy is not installed
    /// </summary>
    private QaCheck IsDriverEasyNotInstalled()
    {
        QaCheck qaCheck = new QaCheck("drivereasy_not_installed");
        const string powershellCommand = "Get-ItemProperty -Path \"HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*\" | Where-Object { $_.DisplayName -Like \"*Driver Easy*\" }";

        string? output = CommandLine.RunProgramWithOutput("powershell.exe", $"-NoProfile -ExecutionPolicy unrestricted -Command \"{powershellCommand}\"");

        if (string.IsNullOrWhiteSpace(output))
        {
            qaCheck.SetPassed("Could not find DriverEasy");
            return qaCheck;
        }
        
        string[] lines = output.Split('\n');
        foreach (string line in lines)
        {
            if (line.Contains("InstallLocation"))
            {
                string[] split = line.Split("InstallLocation");
                qaCheck.SetFailed("Found DriverEasy at location: " + split[1]);

                return qaCheck;
            }
        }
        
        qaCheck.SetFailed("Unknown failure");
        return qaCheck;
    }
    
    /// <summary>
    /// Checks that NVIDIA is not installed
    /// </summary>
    private QaCheck IsNvidiaNotInstalled()
    {
        QaCheck qaCheck = new QaCheck("nvidia_not_installed");
        const string powershellCommand = "Get-ItemProperty -Path \"HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*\" | Where-Object { $_.DisplayName -Like \"*NVIDIA*\" }";

        string? output = CommandLine.RunProgramWithOutput("powershell.exe", $"-NoProfile -ExecutionPolicy unrestricted -Command \"{powershellCommand}\"");

        if (string.IsNullOrWhiteSpace(output))
        {
            qaCheck.SetPassed("Could not find any NVIDIA programs");
            return qaCheck;
        }
        
        string[] lines = output.Split('\n');
        foreach (string line in lines)
        {
            if (line.Contains("InstallLocation"))
            {
                string[] split = line.Split("InstallLocation");
                qaCheck.SetFailed("Found NVIDIA program at location (there may be multiple programs installed): " + split[1]);

                return qaCheck;
            }
        }
        
        qaCheck.SetFailed("Unknown failure");
        return qaCheck;
    }
    
    /// <summary>
    /// Check the System Variables for the OPENSSL_ia32cap entry, check if it is there and also what the value is currently
    /// set to.
    /// </summary>
    private QaCheck CheckEnvAsync()
    {
        QaCheck qaCheck = new QaCheck("openssl_environment");
        string variableName = "OPENSSL_ia32cap";
        string? variableValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);
        if (variableName != null && variableName.Equals("~0x20000000"))
        {
            qaCheck.SetPassed(null);
        }
        else
        {
            qaCheck.SetFailed("OPENSSL_ia32cap was not set or set to the wrong value. Value: " + variableValue);
        }

        return qaCheck;
    }
    
    /// <summary>
    /// Read the registry desktop entry to collect the name of the image used as the current wallpaper.
    /// </summary>
    private QaCheck CheckWallpaper()
    {
        QaCheck qaCheck = new QaCheck("wallpaper_is_set");
        try
        {
            ManagementScope scope = new ManagementScope(@"\\.\root\CIMv2");
            ObjectQuery query = new ObjectQuery("SELECT Wallpaper FROM Win32_Desktop");

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
            {
                ManagementObjectCollection results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    if (obj["Wallpaper"] != null)
                    {
                        string wallpaperPath = System.IO.Path.GetFileName(obj["Wallpaper"].ToString());
                        string wallpaperName = (System.IO.Path.GetFileName(wallpaperPath) ?? "Unknown");
                        // todo - what should it be set to based on the station id
                        if (wallpaperName.Contains(
                                $"Station {Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}") ||
                            wallpaperName.Contains(
                                $"Station{Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process)}"))
                        {
                            qaCheck.SetPassed("Wallpaper is set to: " + wallpaperName);
                        }
                        else
                        {
                            qaCheck.SetWarning("Wallpaper is set to: " + wallpaperName);
                        }
                        
                        return qaCheck;
                    }
                }
            }

            qaCheck.SetFailed("Could not find wallpaper");
        }
        catch (Exception ex)
        {
            qaCheck.SetFailed($"Error: {ex.Message}");
        }

        return qaCheck;
    }

    /// <summary>
    /// Check the local computers currently set timezone.
    /// </summary>
    private QaCheck CheckTimezone()
    {
        QaCheck qaCheck = new QaCheck("timezone_correct");
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
        qaCheck.SetPassed("Timezone is set to: " + localTimeZone); // todo - need to find the correct timezone
        return qaCheck;
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

        string? stdout = CommandLine.RunProgramWithOutput("cmd.exe", $"/C {command}");
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

        string? stdout = CommandLine.RunProgramWithOutput("cmd.exe", $"/C {command}");
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
    /// Checks the accuracy of the system time and date by comparing it with an online NTP server.
    /// </summary>
    /// <returns>A message indicating whether the system time is accurate or inaccurate, or an error message in case of an error.</returns>
    private QaCheck CheckTimeAndDate()
    {
        QaCheck qaCheck = new QaCheck("correct_datetime");
        if (!Network.CheckIfConnectedToInternet())
        {
            qaCheck.SetWarning("Unable to confirm datetime as not connected to internet");
            return qaCheck;
        }

        try
        {
            // Get the current system time in Unix timestamp
            long localTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Fetch the current time from an online NTP server
            string onlineTimeUrl = "http://worldtimeapi.org/api/ip";
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.GetAsync(onlineTimeUrl).Result;
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().Result;

                // Parse the online time data
                dynamic? onlineTimeData = JsonConvert.DeserializeObject(responseBody);
                long onlineUnixTime = onlineTimeData?.unixtime * 1000; // Convert seconds to milliseconds

                // Calculate the time difference in milliseconds
                long timeDifferenceMs = Math.Abs(localTime - onlineUnixTime);

                // Define a threshold for acceptable time difference (e.g., 10 seconds)
                long acceptableTimeDifferenceMs = 5000;

                Console.WriteLine(timeDifferenceMs);
                
                // Compare the time difference with the acceptable threshold
                if (timeDifferenceMs <= acceptableTimeDifferenceMs)
                {
                    qaCheck.SetPassed(null);
                }
                else
                {
                    qaCheck.SetFailed(
                        $"Time did not match current world time. Local time is {UnixTimeStampToDateTime(localTime)} and online time is {UnixTimeStampToDateTime(onlineUnixTime)}");
                }
            }
        }
        catch (Exception ex)
        {
            qaCheck.SetFailed($"Error: {ex.Message}");
        }

        return qaCheck;
    }
    
    private static DateTime UnixTimeStampToDateTime( double unixTimeStamp )
    {
        // Unix timestamp is seconds past epoch
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
    
    private QaCheck CanAccessStationHeroku()
    {
        QaCheck qaCheck = new QaCheck("can_access_station_hosting");
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync("http://learninglablauncher.herokuapp.com/program-station-version").GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                qaCheck.SetPassed(null);
            }
            else
            {
                qaCheck.SetFailed("Accessing station heroku failed with status code: " + response.StatusCode);
            }
        }
        catch (Exception e)
        {
            qaCheck.SetFailed("Accessing station heroku failed with exception: " + e.ToString());
        }

        return qaCheck;
    }
    
    private QaCheck CanAccessLauncherHeroku()
    {
        QaCheck qaCheck = new QaCheck("can_access_nuc_hosting");
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync("http://electronlauncher.herokuapp.com/static/electron-launcher/latest.yml").GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                qaCheck.SetPassed(null);
            }
            else
            {
                qaCheck.SetFailed("Accessing launcher heroku failed with status code: " + response.StatusCode);
            }
        }
        catch (Exception e)
        {
            qaCheck.SetFailed("Accessing launcher heroku failed with exception: " + e.ToString());
        }

        return qaCheck;
    }
}