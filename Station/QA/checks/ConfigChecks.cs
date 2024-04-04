using System;
using System.Collections.Generic;
using Station.MVC.Controller;

namespace Station.QA.checks;

public class ConfigChecks
{
    /**
     * Used to compare against the saved values in the station_list.json
     */
    public List<QaDetail> GetLocalStationDetails()
    {
        List<QaDetail> qaDetails = new()
        {
            new QaDetail("id", GetStationId()),
            new QaDetail("room", GetStationRoom()),
            new QaDetail("labLocation", GetLabLocation()),
            new QaDetail("ipAddress", MainController.localEndPoint.Address.ToString()),
            new QaDetail("macAddress", MainController.macAddress),
            new QaDetail("nucIpAddress", GetExpectedNucAddress()),
            new QaDetail("selectedHeadset", GetSelectedHeadset())
        };

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