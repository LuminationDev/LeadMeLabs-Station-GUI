using System.Collections.Generic;

namespace Station
{
    public interface Wrapper
    {
        List<string>? CollectApplications();
        void PassMessageToProcess(string message);
        void WrapProcess(string processName);
        void ListenForClose();
        bool? CheckCurrentProcess();
        void StopCurrentProcess();
        void RestartCurrentProcess();
        void RestartCurrentSession();
    }
}
