using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Station
{
    public class InternalWrapper : Wrapper
    {
        public const string WrapperType = "Internal";
        //Track any internal executables in the dictionary to start/stop at will
        Dictionary<string, Process> InternalProcesses = new();

        public Experience? GetLastExperience()
        {
            return null;
        }
        
        public void SetLastExperience(Experience experience)
        {
            throw new NotImplementedException();
        }

        public string? GetCurrentExperienceName()
        {
            return null;
        }

        public bool GetLaunchingExperience()
        {
            throw new NotImplementedException();
        }

        public void SetLaunchingExperience(bool isLaunching)
        {
            throw new NotImplementedException();
        }
        
        public List<string>? CollectApplications()
        {
            throw new NotImplementedException();
        }

        public void CollectHeaderImage(string experienceName)
        {
            throw new NotImplementedException();
        }

        public void PassMessageToProcess(string message)
        {
            throw new NotImplementedException();
        }

        public void SetCurrentProcess(Process process)
        {
            throw new NotImplementedException();
        }

        public string WrapProcess(Experience experience)
        {
            Task.Factory.StartNew(() =>
            {
                if (experience.Name == null || experience.AltPath == null) return;

                string processPath = experience.AltPath;

                if (!File.Exists(processPath))
                {
                    SessionController.PassStationMessage($"StationError,File not found:{processPath}");
                    return;
                }

                Process currentProcess = new Process();
                currentProcess.StartInfo.FileName = processPath;

                if (experience.Parameters != null)
                {
                    currentProcess.StartInfo.Arguments = experience.Parameters;
                }

                currentProcess.Start();

                InternalProcesses.Add(experience.Name, currentProcess);
            });
            return "launching";
        }

        public void ListenForClose()
        {
            throw new NotImplementedException();
        }

        public bool? CheckCurrentProcess()
        {
            throw new NotImplementedException();
        }

        public void StopAProcess(Experience experience)
        {
            if (experience.Name == null) return;

            Process? runningProcess;
            InternalProcesses.TryGetValue(experience.Name, out runningProcess);

            if (runningProcess == null) return;

            runningProcess.Kill(true);
        }

        public void StopCurrentProcess()
        {
            throw new NotImplementedException();
        }

        public void RestartCurrentExperience()
        {
            throw new NotImplementedException();
        }
        
        public bool HasCurrentProcess()
        {
            throw new NotImplementedException();
        }
        
        public bool LaunchFailedFromOpenVrTimeout()
        {
            return false;
        }
    }
}
