using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LeadMeLabsLibrary.Station;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._openvr;
using Station.Components._utils._steamConfig;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;

namespace Station.Components._profiles._headsets;

public class SteamLink : Profile, IVrHeadset
{
    private bool _restartingSteamVr;
    private Statuses Statuses { get; } = new();

    public Statuses GetStatusManager()
    {
        return Statuses;
    }

    public DeviceStatus GetHeadsetManagementSoftwareStatus()
    {
        return Statuses.SoftwareStatus;
    }

    public string GetHeadsetManagementProcessName()
    {
        return "vrmonitor";
    }

    public List<string> GetProcesses(ProcessListType type)
    {
        switch (type)
        {
            case ProcessListType.Query:
            case ProcessListType.Minimize:
                return new List<string> { "vrmonitor", "steam", "steamwebhelper" };
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
    }

    public async void MonitorVrConnection()
    {
        if (_restartingSteamVr) return;

        var directory = new DirectoryInfo(@"C:\Program Files (x86)\Steam\logs");
        var file = directory.GetFiles()
            .Where(f => f.Name.Contains("vrserver"))
            .OrderByDescending(f => f.LastWriteTime)
            .First();

        bool containsConnectionDetails = false; // Flag to track if the string is found
        ReverseLineReader reverseLineReader = new ReverseLineReader(file.FullName, Encoding.UTF8);
        IEnumerator<string?> enumerator = reverseLineReader.GetEnumerator();
        do
        {
            string? current = enumerator.Current;
            if (current == null) continue;
            if (!current.Contains("vrlink: Connection inactive") && !current.Contains("vrlink: New Session detected")) continue;
            containsConnectionDetails = true;

            switch (Statuses.SoftwareStatus)
            {
                case DeviceStatus.Connected when current.Contains("Connection inactive"):
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);

                    _restartingSteamVr = true;
                    await OpenVrManager.RestartSteamVr();
                    _restartingSteamVr = false;
                    break;

                case DeviceStatus.Off when current.Contains("Connection inactive"):
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
                    break;

                case DeviceStatus.Lost or DeviceStatus.Off when current.Contains("New Session detected"):
                    Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Connected);
                    break;
            }
            enumerator.Dispose();
        } while (enumerator.MoveNext());

        //The software is running but no headset has connected yet.
        if (!containsConnectionDetails)
        {
            Statuses.UpdateHeadset(VrManager.Software, DeviceStatus.Lost);
        }
    }

    public void WaitForSteamLink()
    {

    }

    public void StopProcessesBeforeLaunch()
    {
        //Not currently required for SteamLink
    }
}
