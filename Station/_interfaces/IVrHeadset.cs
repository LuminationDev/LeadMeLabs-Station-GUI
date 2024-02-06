using System.Collections.Generic;
using Station._profiles._headsets;

namespace Station._interfaces;

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

    void StartVrSession();

    void MonitorVrConnection();

    void StopProcessesBeforeLaunch();
}
