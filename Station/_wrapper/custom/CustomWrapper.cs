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
                WrapperManager.applicationList.TryGetValue(experienceID, out var experience);
                string? experienceName = experience.Name;
                string? altPath = experience.AltPath;

                if (CommandLine.stationLocation == null)
                {
                    MockConsole.WriteLine($"Station working directory not found while searching for header file", MockConsole.LogLevel.Error);
                    SessionController.PassStationMessage($"StationError,Station working directory not found while searching for header file.");

                    Manager.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
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
                    Manager.SendResponse("Android", "Station", $"ThumbnailError:{experienceName}");
                    return;
                }

                //Add the header image to the sending image queue through action transformation
                SocketFile socketImage = new("image", experienceName, filePath);
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

            //Close any open custom processes before opening the next one
            if(currentProcess != null)
            {
                MockConsole.WriteLine($"Closing existing process: {lastExperience.Name}", MockConsole.LogLevel.Normal);
                currentProcess.Kill(true);
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
            while (child == null && attempts < 20)
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
                WindowManager.MaximizeProcess(child); //Maximise the process experience
                SessionController.PassStationMessage($"ApplicationUpdate,{lastExperience.Name}/{lastExperience.ID}/Custom");
                MockConsole.WriteLine($"Application launching: {currentProcess?.MainWindowTitle}/{lastExperience.ID}", MockConsole.LogLevel.Normal);

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
            string altPathWithoutExe = Path.GetDirectoryName(lastExperience.AltPath);
            Logger.WriteLog($"Attempting to get id for " + altPathWithoutExe, MockConsole.LogLevel.Debug);
            string id = CommandLine.GetProcessIdFromDir(altPathWithoutExe);
            if (id == null || id == "")
            {
                return null;
            }
            Process proc = Process.GetProcessById(Int32.Parse(id));

            //Get the steam process name from the CommandLine function and compare here instead of removing any external child processes
            if (proc != null)
            {
                Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{lastExperience.ID}", MockConsole.LogLevel.Debug);
                UIUpdater.UpdateProcess(proc.MainWindowTitle);
                UIUpdater.UpdateStatus("Running...");
                
                return proc;
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
        }

        public void RestartCurrentExperience()
        {
            if (currentProcess != null && !lastExperience.IsNull())
            {
                currentProcess.Kill(true);
                Task.Delay(3000).Wait();
                WrapProcess(lastExperience);
            }
        }
    }
}
