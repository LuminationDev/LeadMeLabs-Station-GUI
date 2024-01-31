using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeadMeLabsLibrary.Station;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._utils._steamConfig;
using Station.MVC.Controller;

namespace Station.Components._profiles._headsets;

public class VivePro1 : IVrHeadset
{
    private Statuses Statuses { get; } = new();

    /// <summary>
    /// The absolute path of the ViveWireless executable on the local machine.
    /// </summary>
    private const string Vive = "C:/Program Files/VIVE Wireless/ConnectionUtility/HtcConnectionUtility.exe";

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
        return "HtcConnectionUtility";
    }

    public List<string> GetProcesses(ProcessListType type)
    {
        switch (type)
        {
            case ProcessListType.Query:
            case ProcessListType.Minimize:
                return new List<string> { "vrmonitor", "steam", "HtcConnectionUtility", "steamwebhelper" };
            default:
                throw new ArgumentException("Invalid process list type.");
        }
    }

    public void StartVrSession()
    {
        CommandLine.KillSteamSigninWindow();
        SteamConfig.VerifySteamConfig();
        CommandLine.StartProgram(SessionController.Steam, " -login " + 
            Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
            Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/250820"); //Open up steam and run steamVR
        CommandLine.StartProgram(Vive); //Start ViveWireless up
    }

    public void MonitorVrConnection()
    {
        var directory = new DirectoryInfo(@"C:\ProgramData\VIVE Wireless\ConnectionUtility\Log");
        var file = directory.GetFiles()
            .OrderByDescending(f => f.LastWriteTime)
            .First();
        ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.Unicode);
        IEnumerator<string?> enumerator = reverseLineReader.GetEnumerator();
        do
        {
            string? current = enumerator.Current;
            if (current == null)
            {
                continue;
            }
            if (current.Contains("Terminated"))
            {
                enumerator.Dispose();
                Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Off);
                return;
            }

            if (!current.Contains("Connection Status set to")) continue;
            switch (Statuses.SoftwareStatus)
            {
                case DeviceStatus.Connected or DeviceStatus.Off when current.Contains("CONNECTION_STATUS_SCANNING"):
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                    break;
                
                case DeviceStatus.Lost or DeviceStatus.Off when current.Contains("CONNECTION_STATUS_CONNECTED"):
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
                    break;
            }
            enumerator.Dispose();
        } while (enumerator.MoveNext());
    }

    /// <summary>
    /// Kill off the Steam VR process.
    /// </summary>
    public async void StopProcessesBeforeLaunch()
    {
        CommandLine.QueryProcesses(new List<string> { "vrmonitor" }, true);
        
        await Task.Delay(3000);
    }
}
