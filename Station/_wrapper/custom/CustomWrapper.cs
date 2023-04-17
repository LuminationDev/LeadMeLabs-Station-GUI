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
        private static Experience lastExperience;

        public string? GetCurrentExperienceName()
        {
            return lastExperience.Name;
        }

        public List<string>? CollectApplications()
        {
            return CustomScripts.loadAvailableGames();
        }

        public void CollectHeaderImage(string experienceID)
        {
            Task.Factory.StartNew(() =>
            {
                WrapperManager.applicationList.TryGetValue(int.Parse(experienceID), out var experience);
                string? experienceName = experience.Name;
                string? altPath = experience.AltPath;

                if (CommandLine.stationLocation == null)
                {
                    MockConsole.WriteLine($"Station working directory not found while searching for header file", MockConsole.LogLevel.Error);
                    SessionController.PassStationMessage($"StationError,Station working directory not found while searching for header file.");

                    Manager.sendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                    return;
                }

                //Determine if it was imported or downloaded and find the header file
                string filePath;
                if (altPath != null)
                {
                    string parentFolder = CustomScripts.GetParentDirPath(altPath);
                    filePath = parentFolder + @"\header.jpg";
                } else
                {
                    filePath = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..\..", $"leadme_apps\\{experienceName}\\header.jpg"));
                }

                if (!File.Exists(filePath))
                {
                    MockConsole.WriteLine($"File not found:{filePath}", MockConsole.LogLevel.Error);
                    SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                    Manager.sendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                    return;
                }

                //Add the header image to the sending image queue through action transformation
                SocketImage socketImage = new(experienceName, filePath);
                System.Action sendImage = new(() => socketImage.send());

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

        public void WrapProcess(Experience experience)
        {
            if(CommandLine.stationLocation == null)
            {
                SessionController.PassStationMessage("Cannot find working directory");
                return;
            }

            MockConsole.WriteLine($"Launching process: {experience.Name}", MockConsole.LogLevel.Normal);
            Task.Factory.StartNew(() =>
            {
                string filePath;

                //The existance of an Alternate path means the experience has been imported through the launcher application
                if (experience.AltPath != null)
                {
                    filePath = experience.AltPath;
                }
                else
                {
                    //TODO Currently the exe and folder need to be the same name
                    filePath = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..\..", $"leadme_apps\\{experience.Name}\\{experience.Name}.exe"));
                }

                if(!File.Exists(filePath)) {
                    SessionController.PassStationMessage($"StationError,File not found:{filePath}");
                    return;
                }

                lastExperience = experience;

                currentProcess = new Process();
                currentProcess.StartInfo.FileName = filePath;

                if (experience.Parameters != null)
                {
                    currentProcess.StartInfo.Arguments = experience.Parameters;
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

            if (child != null && currentProcess != null && lastExperience.ExeName != null)
            {
                UIUpdater.UpdateProcess(lastExperience.Name);
                UIUpdater.UpdateStatus("Running...");

                SessionController.PassStationMessage($"ApplicationUpdate,{lastExperience.Name}/{currentProcess?.Id}/Custom");
                MockConsole.WriteLine($"Application launching: {currentProcess?.MainWindowTitle}/{currentProcess?.Id}", MockConsole.LogLevel.Normal);

                ListenForClose();
            }
            else
            {
                StopCurrentProcess();
                UIUpdater.ResetUIDisplay();
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{lastExperience.Name}");
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
                if (proc.MainWindowTitle.Contains(lastExperience.ExeName))
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
                //WrapperManager.ClosePipeServer();
            }
            CommandLine.queryVRProcesses(WrapperMonitoringThread.steamProcesses, true);
        }

        public void RestartCurrentProcess()
        {
            if (currentProcess != null && !lastExperience.IsNull())
            {
                currentProcess.Kill(true);
                Task.Delay(3000).Wait();
                WrapProcess(lastExperience);
            }
        }

        public async void RestartCurrentSession()
        {
            SessionController.PassStationMessage("Processing,false");

            SessionController.PassStationMessage("MessageToAndroid,SetValue:session:Restarted");
        }
    }
}
