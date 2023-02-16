using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Station
{
    public class InternalWrapper : Wrapper
    {
        public static string wrapperType = "Internal";
        //Track any internal executables in the dictionary to start/stop at will
        Dictionary<string, Process> InternalProcesses = new();

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

        public void WrapProcess(string processPath, string? launchParameters = null)
        {
            Task.Factory.StartNew(() =>
            {
                if (!File.Exists(processPath))
                {
                    SessionController.PassStationMessage($"StationError,File not found:{processPath}");
                    return;
                }

                string key = Path.GetFileNameWithoutExtension(processPath);

                Process currentProcess = new Process();
                currentProcess.StartInfo.FileName = processPath;

                if (launchParameters != null)
                {
                    currentProcess.StartInfo.Arguments = launchParameters;
                }

                currentProcess.Start();

                InternalProcesses.Add(key, currentProcess);
            });
        }

        public void ListenForClose()
        {
            throw new NotImplementedException();
        }

        public bool? CheckCurrentProcess()
        {
            throw new NotImplementedException();
        }

        public void StopAProcess(string processPath)
        {
            Process? runningProcess;
            InternalProcesses.TryGetValue(processPath, out runningProcess);

            if (runningProcess == null) return;

            runningProcess.Kill(true);
        }

        public void StopCurrentProcess()
        {
            throw new NotImplementedException();
        }

        public void RestartCurrentProcess()
        {
            throw new NotImplementedException();
        }

        public void RestartCurrentSession()
        {
            throw new NotImplementedException();
        }
    }
}
