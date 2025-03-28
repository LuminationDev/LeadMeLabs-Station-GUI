﻿using System;
using System.Collections.Generic;
using System.Management;
using System.Net.Http;
using Newtonsoft.Json;
using Station.Components._commandLine;
using Station.Components._utils;

namespace Station.QA.checks;

public class WindowChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa(string labType)
    {
        _qaChecks = new List<QaCheck>
        {
            GetWindowsVersion(),
            IsWakeOnMagicPacketEnabled(),
            CheckEnvAsync(),
            CheckWallpaper()
        };

        if (labType.Equals("Online"))
        {
            _qaChecks.Add(CheckTimezone());
            _qaChecks.Add(CheckTimeAndDate());
        }

        return _qaChecks;
    }
    
    /// <summary>
    /// Return a QA detail of the Windows version
    /// </summary>
    private QaCheck GetWindowsVersion()
    {
        QaCheck qaCheck = new QaCheck("windows_version_detail");
        string version = Environment.OSVersion.Version.ToString();
        qaCheck.SetDetail(version);
        return qaCheck;
    }
    
    /// <summary>
    /// Query the main network adapter (this should only be one, however test this)
    /// </summary>
    private QaCheck IsWakeOnMagicPacketEnabled()
    {
        QaCheck qaCheck = new QaCheck("magic_packet_enabled");
        const string powershellCommand = "Get-NetAdapterAdvancedProperty -Name '*' -RegistryKeyword '*WakeOnMagicPacket' | Select-Object -Property Name, DisplayName, DisplayValue";

        string? output = StationCommandLine.RunProgramWithOutput("powershell.exe", $"-NoProfile -ExecutionPolicy unrestricted -Command \"{powershellCommand}\"");

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
    /// Check the System Variables for the OPENSSL_ia32cap entry, check if it is there and also what the value is currently
    /// set to.
    /// </summary>
    private QaCheck CheckEnvAsync()
    {
        QaCheck qaCheck = new QaCheck("openssl_environment");
        string variableName = "OPENSSL_ia32cap";
        string? variableValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);
        if (variableValue != null && variableValue.Equals("~0x20000000"))
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
                foreach (var o in results)
                {
                    ManagementObject obj = (ManagementObject)o;
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
    /// Checks the accuracy of the system time and date by comparing it with an online NTP server.
    /// </summary>
    /// <returns>A message indicating whether the system time is accurate or inaccurate, or an error message in case of an error.</returns>
    private QaCheck CheckTimeAndDate()
    {
        QaCheck qaCheck = new QaCheck("correct_datetime");
        if (!Network.CheckIfConnectedToInternet(true))
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
}