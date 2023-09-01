using System;

namespace Station._qa.checks;

public class ConfigInfo
{
    public string? SelectedHeadset { get; set; }
    public string? SteamDetailsPresent { get; set; }
    public string? LabLocation { get; set; }
    public string? StationId { get; set; }
    public string? NucIpAddress { get; set; }
}

public class ConfigChecks
{
    public ConfigInfo? GetLocalConfigurationDetails()
    {
        ConfigInfo configInfo = new ConfigInfo
        {
            SelectedHeadset = GetSelectedHeadset(),
            SteamDetailsPresent = AreSteamDetailsPresent(),
            LabLocation = GetLabLocation(),
            StationId = GetStationId(),
            NucIpAddress = GetExpectedNucAddress()
        };
        
        return configInfo;
    }

    /// <summary>
    /// Return the current Headset type, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetSelectedHeadset()
    {
        return Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process) ?? "Not set";
    }

    /// <summary>
    /// Check if the Steam username and password are present in the EnvironmentVariableTarget.Process, do not send these
    /// back. Just confirm they are there. The SoftwareChecks contains code to check if they are correct.
    /// </summary>
    private string AreSteamDetailsPresent()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process)))
        {
            return "Username or password are not set";
        }

        return "Details are present";
    }
    
    /// <summary>
    /// Return the current Lab Location, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetLabLocation()
    {
        return Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Not set";
    }
    
    /// <summary>
    /// Return the current Station ID, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetStationId()
    {
        return Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Not set";
    }
    
    /// <summary>
    /// Return the current expected Nuc Address, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetExpectedNucAddress()
    {
        return Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process) ?? "Not set";
    }
}