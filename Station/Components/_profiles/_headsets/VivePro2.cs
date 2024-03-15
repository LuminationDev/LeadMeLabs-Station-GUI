using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._utils._steamConfig;
using Station.MVC.Controller;

namespace Station.Components._profiles._headsets;

public class VivePro2 : Profile, IVrHeadset
{
    private Statuses Statuses { get; } = new();

    public Statuses GetStatusManager()
    {
        return Statuses;
    }

    /// <summary>
    /// If the headset is managed by more than just OpenVR return the management software connection
    /// status. In this case it is managed by Vive Console.
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
        return "LhStatusMonitor";
    }

    public List<string> GetProcesses(ProcessListType type)
    {
        switch (type)
        {
            case ProcessListType.Query:
                return new List<string> { "vrmonitor", "steam", "HtcConnectionUtility", "steamwebhelper" };
            case ProcessListType.Minimize:
                return new List<string> { "vrmonitor", "steam", "LhStatusMonitor", "WaveConsole", "steamwebhelper" };
            default:
                throw new ArgumentException("Invalid process list type.");
        }
    }

    public void StartVrSession()
    {
        CommandLine.KillSteamSigninWindow();
        SteamConfig.VerifySteamConfig();
        CommandLine.StartProgram(SessionController.Steam, "-noreactlogin -login " +
            Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " +
            Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/1635730"); //Open up steam and run vive console
    }

    public void MonitorVrConnection()
    {
        Process[] vivePro2Connector = ProcessManager.GetProcessesByName("WaveConsole");
        if (vivePro2Connector.Length > 0)
        {
            if (vivePro2Connector.Any(process => process.MainWindowTitle.Equals("VIVE Console")))
            {
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                return;
            }
        }
        
        Process[] viveStatusMonitor = ProcessManager.GetProcessesByName("LhStatusMonitor");
        if (viveStatusMonitor.Length > 0)
        {
            Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
            return;
        }
        Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
    }

    /// <summary>
    /// Kill off the Steam VR process.
    /// </summary>
    public void StopProcessesBeforeLaunch()
    {
        //Not currently required for VivePro2
    }
}
