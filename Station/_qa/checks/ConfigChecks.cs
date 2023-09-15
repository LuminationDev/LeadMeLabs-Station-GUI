using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Station._qa.checks;

public class ConfigChecks
{
    /**
     * Used to compare against the saved values in the station_list.json
     */
    public List<QaDetail> GetLocalStationDetails()
    {
        List<QaDetail> qaDetails = new();
        qaDetails.Add(new QaDetail("id", GetStationId()));
        qaDetails.Add(new QaDetail("room", GetStationRoom()));
        qaDetails.Add(new QaDetail("labLocation", GetLabLocation()));
        qaDetails.Add(new QaDetail("ipAddress", Manager.localEndPoint.Address.ToString()));
        qaDetails.Add(new QaDetail("macAddress", Manager.macAddress));
        qaDetails.Add(new QaDetail("nucIpAddress", GetExpectedNucAddress()));
        qaDetails.Add(new QaDetail("selectedHeadset", GetSelectedHeadset()));

        return qaDetails;
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
    /// Return the room the Station belongs to, only load using the EnvironmentVariableTarget.Process, to disregard any saved
    /// local ENVs.
    /// </summary>
    private string GetStationRoom()
    {
        return Environment.GetEnvironmentVariable("room", EnvironmentVariableTarget.Process) ?? "Not set";
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