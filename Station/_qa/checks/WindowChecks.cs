using System;
using System.Management;
using System.Net.Http;
using LeadMeLabsLibrary;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Station._qa.checks;

public class WindowsInfo
{
    public string? MagicPacket { get; set; }
    public string? AmdInstalled { get; set; }
    public string? OpensslEnv { get; set; }
    public string? WallPaper { get; set; }
    public string? TimeZone { get; set; }
    public string? SystemTime { get; set; }
    public string? Firewall { get; set; }
}

public class WindowChecks
{
    public string? GetLocalOsSettings()
    {
        WindowsInfo windowInfo = new WindowsInfo
        {
            MagicPacket = QueryNetworkAdapter(), //Move to LeadMeLibrary
            AmdInstalled = IsAmdInstalled(),
            OpensslEnv = CheckEnvAsync(),
            WallPaper = CheckWallpaper(), //Move to LeadMeLibrary
            TimeZone = CheckTimezone(), //Move to LeadMeLibrary
            SystemTime = CheckTimeAndDate(), //Move to LeadMeLibrary
            Firewall = FirewallManagement.IsProgramAllowedThroughFirewall() ?? "Unknown"
        };
        
        return windowInfo.ToString();
    }
    
    /// <summary>
    /// Query the main network adapter (this should only be one, however test this)
    /// </summary>
    private string QueryNetworkAdapter()
    {
        const string powershellCommand = "Get-NetAdapterAdvancedProperty -Name '*' -RegistryKeyword '*WakeOnMagicPacket' | Select-Object -Property Name, DisplayName, DisplayValue";

        string? output = CommandLine.RunProgramWithOutput("powershell.exe", $"-NoProfile -ExecutionPolicy unrestricted -Command \"{powershellCommand}\"");

        if (output == null)
        {
            return "Display value not found.";
        }
        
        string[] lines = output.Split('\n');
        foreach (string line in lines)
        {
            if (line.Contains("Wake on Magic Packet"))
            {
                string[] split = line.Split("Wake on Magic Packet");
                return split[0].Trim() + ":Wake on Magic Packet:" + split[1].Trim();
            }
        }
        
        return "Display value not found.";
    }
    
    /// <summary>
    /// Checks if AMD Adrenalin is installed on the system.
    /// </summary>
    private string IsAmdInstalled()
    {
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
                                    return "Installed";
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("AMD Adrenalin is not installed.");
            return "Not installed";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Check the System Variables for the OPENSSL_ia32cap entry, check if it is there and also what the value is currently
    /// set to.
    /// </summary>
    private string CheckEnvAsync()
    {
        string variableName = "OPENSSL_ia32cap";
        string? variableValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);
        return string.IsNullOrWhiteSpace(variableValue) ? "Undefined" : variableValue;
    }
    
    /// <summary>
    /// Read the registry desktop entry to collect the name of the image used as the current wallpaper.
    /// </summary>
    private string CheckWallpaper()
    {
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
                        string wallpaperPath = obj["Wallpaper"].ToString();
                        return System.IO.Path.GetFileName(wallpaperPath) ?? "Unknown";
                    }
                }
            }

            return "Unknown";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Check the local computers currently set timezone.
    /// </summary>
    private string CheckTimezone()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
        return localTimeZone.DisplayName;
    }
    
    /// <summary>
    /// Checks the accuracy of the system time and date by comparing it with an online NTP server.
    /// </summary>
    /// <returns>A message indicating whether the system time is accurate or inaccurate, or an error message in case of an error.</returns>
    private string CheckTimeAndDate()
    {
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
                long acceptableTimeDifferenceMs = 10000;

                Console.WriteLine(timeDifferenceMs);
                
                // Compare the time difference with the acceptable threshold
                if (timeDifferenceMs <= acceptableTimeDifferenceMs)
                {
                    return "System time is accurate.";
                }

                return "System time is inaccurate.";
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}