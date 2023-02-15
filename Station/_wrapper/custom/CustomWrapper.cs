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
        private static string? gameName = null;

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

            MockConsole.WriteLine($"Launching process: {processName}", MockConsole.LogLevel.Normal);
            Task.Factory.StartNew(() =>
            {
                //TODO Current the exe and folder need to be the same name
                string filePath = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..\..", $"leadme_apps\\{processName}\\{processName}.exe"));

                if(!File.Exists(filePath)) {
                    SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                    return;
                }

                gameName = processName;

                currentProcess = new Process();
                currentProcess.StartInfo.FileName = filePath;
                currentProcess.Start();

                FindCurrentProcess();
            });                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
        }

        /// <summary>
        /// Find the active process that has been launched.
        /// </summary>
        public void FindCurrentProcess()
        {
            int attempts = 0; //Track the loop for finding child processes

            Process? child = GetExperienceProcess();
            while (child == null && attempts < 10)
            {
                attempts++;
                MockConsole.WriteLine($"Checking for child process...", MockConsole.LogLevel.Debug);
                Task.Delay(3000).Wait();
                child = GetExperienceProcess();
            }
            currentProcess = child;

            if (child != null && currentProcess != null && gameName != null)
            {
                UIUpdater.UpdateProcess(gameName);
                UIUpdater.UpdateStatus("Running...");

                SessionController.PassStationMessage($"ApplicationUpdate,{currentProcess?.MainWindowTitle}/{currentProcess?.Id}");
                MockConsole.WriteLine($"Application launching: {currentProcess?.MainWindowTitle}/{currentProcess?.Id}", MockConsole.LogLevel.Normal);

                ListenForClose();
            }
            else
            {
                StopCurrentProcess();
                UIUpdater.ResetUIDisplay();
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{gameName}");
            }
        }

        /// <summary>
        /// Scan the active processes that to find the launched application. The process is matched by the 
        /// MainWindowTitle that is represented by the Custom application name.
        /// </summary>
        /// <returns>The launched application process</returns>
        private Process? GetExperienceProcess()
        {
            Process[] processes = Process.GetProcesses();

            foreach (var proc in processes)
            {
                //Get the steam process name from the CommandLine function and compare here instead of removing any external child processes
                if (proc.MainWindowTitle == gameName)
                {
                    MockConsole.WriteLine($"Application found: {proc.MainWindowTitle}/{proc.Id}", MockConsole.LogLevel.Debug);

                    UIUpdater.UpdateProcess(proc.MainWindowTitle);
                    UIUpdater.UpdateStatus("Running...");

                    return proc;
                }
            }

            return null;
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
            CommandLine.queryVRProcesses(WrapperMonitoringThread.steamProcesses, true);
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
