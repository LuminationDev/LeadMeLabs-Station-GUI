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

    public interface VrHeadset
    {
        VrStatus GetStatusManager();

        DeviceStatus GetHeadsetManagementSoftwareStatus();

        bool WaitForConnection(string wrapperType);

        List<string> GetProcessesToQuery();

        void StartVrSession();

        void MinimizeVrProcesses();

        void StopTimer();

        void MonitorVrConnection();

        void StopProcessesBeforeLaunch();
    }
}
