using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LeadMeLabsLibrary;
using LeadMeLabsLibrary.Station;
using Microsoft.Win32;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._utils;
using Station.Components._utils._steamConfig;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;

namespace Station.Components._profiles._headsets;

public class ViveBusinessStreaming : Profile, IVrHeadset
{
    private Statuses Statuses { get; } = new();

    /// <summary>
    /// The absolute path of the Vive Business Streaming executable on the local machine.
    /// </summary>
    private const string Vive = @"C:\Program Files\VIVE Business Streaming\RRConsole\RRConsole.exe";

    public Statuses GetStatusManager()
    {
        return Statuses;
    }

    /// <summary>
    /// If the headset is managed by more than just OpenVR return the management software connection
    /// status. In this case it is managed by Vive Wireless.
    /// </summary>
    /// <returns></returns>
    public DeviceStatus GetHeadsetManagementSoftwareStatus()
    {
        return Statuses.SoftwareStatus;
    }
    
    /// <summary>
    /// Return the process name of the headset management software
    /// </summary>
    /// <returns></returns>
    public string GetHeadsetManagementProcessName()
    {
        return "RRServer";
    }

    public List<string> GetProcesses(ProcessListType type)
    {
        switch (type)
        {
            case ProcessListType.Query:
            case ProcessListType.Minimize:
                return new List<string> { "vrmonitor", "steam", "RRConsole", "RRServer", "steamwebhelper" };
            default:
                throw new ArgumentException("Invalid process list type.");
        }
    }

    public void StartVrSession(bool openDevTools = false)
    {
        StationCommandLine.KillSteamSigninWindow();
        SteamConfig.VerifySteamConfig();
        StationCommandLine.StartProgram(SessionController.Steam, (openDevTools ? " -opendevtools" : "") + " -login " + 
                                                          Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
                                                          Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + (openDevTools ? "" : $" steam://rungameid/{SteamScripts.SteamVrId}")); //Open up steam and run steamVR
        if (!openDevTools)
        {
            StationCommandLine.StartProgram(Vive); //Start Vive business streaming
        }
    }

    public void MonitorVrConnection()
    {
        var registryVal = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\HTC\\VBS", "ServerState", -1);
        if (registryVal == null || registryVal.Equals(-1))
        {
             var directory = new DirectoryInfo(@"C:\ProgramData\HTC\ViveSoftware\ViveRR\Log");
             var file = directory.GetFiles()
                .Where(f => f.Name.Contains("RRConsole"))
                .OrderByDescending(f => f.LastWriteTime)
                .First();
            
            //Check if the file is empty (new or rotated log files)
            FileInfo fileInfo = new FileInfo(file.FullName);
            if (fileInfo.Length < 10)
            {
                Logger.WriteLog($"File is below 10 bytes: {file.FullName}, {fileInfo.Length}", Enums.LogLevel.Debug);
                return;
            }
            
            bool containsOnHmdReady = false; // Flag to track if the string is found
            try
            {
                ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.Unicode);
                IEnumerator<string?> enumerator = reverseLineReader.GetEnumerator();
                do
                {
                    string? current = enumerator.Current;
                    if (current == null) continue;
                
                    //We have reached the top of the log file, and it has been rotated, use the previous known connection as 
                    //no other connection events have occurred since the rotation.
                    if (current.Contains("# Log rotate")) return;
                    if (!current.Contains("OnHMDReady")) continue;
                    containsOnHmdReady = true;
                
                    switch (Statuses.SoftwareStatus)
                    {
                        case DeviceStatus.Connected or DeviceStatus.Off when current.Contains("False"):
                            Logger.WriteLog($"Device lost - Reading: {file.FullName}, {file.Length}", Enums.LogLevel.Debug);
                            Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                            break;
                    
                        case DeviceStatus.Lost or DeviceStatus.Off when current.Contains("True"):
                            Logger.WriteLog($"Device connected - Reading: {file.FullName}, {file.Length}", Enums.LogLevel.Debug);
                            Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
                            break;
                    }
                    enumerator.Dispose();
                } while (enumerator.MoveNext());
                
                if (containsOnHmdReady) return;
                //The software is running but no headset has connected yet.
                Logger.WriteLog($"Attempted reading: {file.FullName}, {file.Length}", Enums.LogLevel.Debug);
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
            }
            catch (InvalidDataException e)
            {
                Logger.WriteLog($"Device lost - InvalidDataException", Enums.LogLevel.Debug);
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
            }
        }
        else
        {
            if (registryVal.Equals(0) && Statuses.SoftwareStatus is DeviceStatus.Lost or DeviceStatus.Off)
            {
                Logger.WriteLog($"Device connected", Enums.LogLevel.Debug);
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
            }
            else if (Statuses.SoftwareStatus is DeviceStatus.Connected or DeviceStatus.Off)
            {
                Logger.WriteLog($"Device lost", Enums.LogLevel.Debug);
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
            }
        }
    }

    /// <summary>
    /// Kill off the Steam VR process.
    /// </summary>
    public void StopProcessesBeforeLaunch()
    {
        //Not currently required for ViveBusinessStreaming
    }
}
