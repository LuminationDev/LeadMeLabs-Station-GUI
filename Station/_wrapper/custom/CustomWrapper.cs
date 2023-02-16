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
        private static string? experienceName = null;

        public List<string>? CollectApplications()
        {
            return CustomScripts.loadAvailableGames();
        }

        public void CollectHeaderImage(string experienceName)
        {
            Task.Factory.StartNew(() =>
            {
                if (CommandLine.stationLocation == null)
                {
                    MockConsole.WriteLine($"Station working directory not found while searching for header file", MockConsole.LogLevel.Error);
                    SessionController.PassStationMessage($"StationError,Station working directory not found while searching for header file.");

                    Manager.sendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                    return;
                }

                //Find the header file
                string filePath = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..\..", $"leadme_apps\\{experienceName}\\header.jpg"));

                if (!File.Exists(filePath))
                {
                    MockConsole.WriteLine($"File not found:{filePath}", MockConsole.LogLevel.Error);
                    SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                    Manager.sendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                    return;
                }

                //Add the header image to the sending image queue through action transformation
                SocketImage socketImage = new(experienceName, filePath);
                Action sendImage = new(() => socketImage.send());

                //Queue the send function for invoking
                TaskQueue.Queue(false, sendImage);

                MockConsole.WriteLine($"Thumbnail for experience: {experienceName} now queued for transfer.", MockConsole.LogLevel.Error);
            });
        }

        public void PassMessageToProcess(string message)
        {
            PipeClient pipeClient = new(MockConsole.WriteLine, 5);

            Task.Factory.StartNew(() =>
            {
                pipeClient.Send(message);
            });
        }

        public void WrapProcess(string processName, string? launchParameters = null)
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

                experienceName = processName;

                currentProcess = new Process();
                currentProcess.StartInfo.FileName = filePath;

                if (launchParameters != null)
                {
                    currentProcess.StartInfo.Arguments = launchParameters;
                }

                currentProcess.Start();

                FindCurrentProcess();
            });                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
        }

        /// <summary>
        /// Find the active process that has been launched. This may have to loop if the main experience is
        /// openning accompanying software such as SteamVR. 
        /// </summary>
        private void FindCurrentProcess()
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

            if (child != null && currentProcess != null && experienceName != null)
            {
                UIUpdater.UpdateProcess(experienceName);
                UIUpdater.UpdateStatus("Running...");

                SessionController.PassStationMessage($"ApplicationUpdate,{currentProcess?.MainWindowTitle}/{currentProcess?.Id}");
                MockConsole.WriteLine($"Application launching: {currentProcess?.MainWindowTitle}/{currentProcess?.Id}", MockConsole.LogLevel.Normal);

                ListenForClose();
            }
            else
            {
                StopCurrentProcess();
                UIUpdater.ResetUIDisplay();
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{experienceName}");
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
                if (proc.MainWindowTitle == experienceName)
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
            if (currentProcess != null && experienceName != null)
            {
                currentProcess.Kill(true);
                Task.Delay(3000).Wait();
                WrapProcess(experienceName);
            }
        }

        public async void RestartCurrentSession()
        {
            throw new NotImplementedException();
        }
    }
}
