using System.Collections.Generic;

namespace Station
{
    public interface VrHeadset
    {
        List<string> GetProcessesToQuery();

        void StartVrSession();

        void MinimizeVrProcesses();

        void StopTimer();

        string MonitorVrConnection(string currentViveStatus);

        string StopProcessesBeforeLaunch();
    }
}
