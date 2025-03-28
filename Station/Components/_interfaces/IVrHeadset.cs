﻿using System.Collections.Generic;
using Station.Components._profiles._headsets;

namespace Station.Components._interfaces;

public enum VrManager
{
    Software, //Third-party software that manages the headset to SteamVR connection
    OpenVR //Steams' VR management software
}

public enum DeviceStatus
{
    Connected, //Vive & OpenVR connection
    Lost, //Vive or OpenVR not tracking
    Off //No Vive connection
}

public enum ProcessListType
{
    Query,
    Minimize
}

public interface IVrHeadset
{
    Statuses GetStatusManager();

    DeviceStatus GetHeadsetManagementSoftwareStatus();
    
    string GetHeadsetManagementProcessName();
    
    List<string> GetProcesses(ProcessListType type);

    void StartVrSession(bool openDevTools = false);

    void MonitorVrConnection();

    void StopProcessesBeforeLaunch();
}
