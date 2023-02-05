using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using leadme_api;

namespace Station
{
    internal class CustomWrapper : Wrapper
    {
        public static string wrapperType = "Custom";
        private static Process? currentProcess;

        public List<string>? CollectApplications()
        {
            return CustomScripts.loadAvailableGames();
        }

        public void PassMessageToProcess(string message)
        {
            PipeClient pipeClient = new(MockConsole.WriteLine, 5);

            Task.Factory.StartNew(() =>
            {
                pipeClient.Send(message);
            });
        }

        public void WrapProcess(string processName)
        {
            if(CommandLine.stationLocation == null)
            {
                SessionController.PassStationMessage("Cannot find working directory");
                return;
            }

            Trace.WriteLine("Launching process");
            Task.Factory.StartNew(() =>
            {
                //TODO Current the exe and folder need to be the same name
                string filePath = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..\..", $"leadme_apps\\{processName}\\{processName}.exe"));

                if(!File.Exists(filePath)) {
                    SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                    return;
                }

                currentProcess = new Process();
                currentProcess.StartInfo.FileName = filePath;
                currentProcess.Start();

                Task.Delay(2000).Wait();

                UIUpdater.UpdateProcess(currentProcess.ProcessName);
                UIUpdater.UpdateStatus("Running...");

                SessionController.PassStationMessage($"ApplicationUpdate,{currentProcess.MainWindowTitle}/{currentProcess?.Id}");

                ListenForClose();
            });
        }

        /// <summary>
        /// Being a new thread with the purpose of detecting if the current process has been exited.
        /// </summary>
        public void ListenForClose()
        {
            Task.Factory.StartNew(() =>
            {
                currentProcess?.WaitForExit();
                Trace.WriteLine("The current process has just exited.");
                SessionController.PassStationMessage($"ApplicationClosed");
                UIUpdater.ResetUIDisplay();
                WrapperManager.RecycleWrapper();
            });
        }

        /// <summary>
        /// Check if a process is currently running.
        /// </summary>
        public bool? CheckCurrentProcess()
        {
            return currentProcess?.Responding;
        }

        /// <summary>
        /// Kill the currently running process, releasing all resources associated with it.
        /// </summary>
        public void StopCurrentProcess()
        {
            if (currentProcess != null)
            {
                currentProcess.Kill();
            }
        }

        public void RestartCurrentProcess()
        {
            throw new NotImplementedException();
        }

        public async void RestartCurrentSession()
        {
            throw new NotImplementedException();
        }
    }
}
