using System;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;

namespace Station.QA.checks;

public class ConfigChecks
{
    /**
     * Used to compare against the saved values in the station_list.json
     */
    public JObject GetLocalStationDetails()
    {
        JObject responseData = new JObject();
        responseData.Add("ipAddress", SystemInformation.GetIPAddress()?.ToString());
        responseData.Add("nucIpAddress", GetExpectedNucAddress());
        responseData.Add("id", GetStationId());
        responseData.Add("labLocation", GetLabLocation());
        responseData.Add("stationMode", GetStationMode());
        responseData.Add("room", GetStationRoom());
        responseData.Add("macAddress", SystemInformation.GetMACAddress());
        
        return responseData;
    }
    
    /// <summary>
    /// Return the room the Station belongs to, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetStationRoom()
    {
        return Environment.GetEnvironmentVariable("room", EnvironmentVariableTarget.Process) ?? "Not found";
    }
    
    /// <summary>
    /// Return the current Lab Location, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetLabLocation()
    {
        return Environment.GetEnvironmentVariable("LabLocation", EnvironmentVariableTarget.Process) ?? "Not found";
    }
    
    /// <summary>
    /// Return the current Station ID, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetStationId()
    {
        return Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "Not found";
    }
    
    /// <summary>
    /// Return the current expected Nuc Address, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetExpectedNucAddress()
    {
        return Environment.GetEnvironmentVariable("NucAddress", EnvironmentVariableTarget.Process) ?? "Not found";
    }

    /// <summary>
    /// Return the currently set Station mode, this may be appliance, content or vr.
    /// </summary>
    private string GetStationMode()
    {
        return Environment.GetEnvironmentVariable("StationMode", EnvironmentVariableTarget.Process) ?? "Not found";
    }
}
