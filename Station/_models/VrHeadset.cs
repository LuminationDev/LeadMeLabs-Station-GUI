using System.Collections.Generic;
using Station._headsets;

namespace Station
{
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

    public interface IVrHeadset
    {
        Statuses GetStatusManager();

        DeviceStatus GetHeadsetManagementSoftwareStatus();
        
        string GetHeadsetManagementProcessName();

        bool WaitForConnection(string wrapperType);

        List<string> GetProcessesToQuery();

        void MinimizeSoftware(int attemptLimit);

        void StartVrSession();

        void MonitorVrConnection();

        void StopProcessesBeforeLaunch();
    }
}
