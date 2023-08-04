using System.Collections.Generic;

namespace Station
{
    public enum HMDStatus
    {
        Connected,
        Lost
    }

    public interface VrHeadset
    {
        HMDStatus GetConnectionStatus();

        void SetOpenVRStatus(HMDStatus status);

        List<string> GetProcessesToQuery();

        void StartVrSession();

        void MinimizeVrProcesses();

        void StopTimer();

        void MonitorVrConnection();

        void StopProcessesBeforeLaunch();
    }
}
