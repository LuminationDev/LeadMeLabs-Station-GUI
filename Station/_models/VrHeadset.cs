using System.Collections.Generic;

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

        void StartVrSession();

        void MonitorVrConnection();

        void StopProcessesBeforeLaunch();
    }
}
