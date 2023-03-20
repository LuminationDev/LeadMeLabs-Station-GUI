using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Station
{
    public class SteamWrapper : Wrapper
    {
        public static string wrapperType = "Steam";
        private static Process? currentProcess;
        private static string launch_params = "-noreactlogin -login " + Environment.GetEnvironmentVariable("SteamUserName") + " " + Environment.GetEnvironmentVariable("SteamPassword") + " steam://rungameid/";
        public static string? experienceName = null;

        /// <summary>
        /// Track if an experience is being launched.
        /// </summary>
        public static bool launchingExperience = false;

        public List<string>? CollectApplications()
        {
            return SteamScripts.loadAvailableGames();
        }

        public void CollectHeaderImage(string experienceName)
        {
            throw new NotImplementedException();
        }

        public void PassMessageToProcess(string message)
        {
            throw new NotImplementedException();
        }

        public void WrapProcess(Experience experience)
        {
            if (experience.ID == null)
            {
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Unknown Experience");
                return;
            };

            SteamScripts.lastApp = experience;
            GetGameProcessName();

            MockConsole.WriteLine($"Wrapping: {experienceName}", MockConsole.LogLevel.Debug);

            //Start the external processes to handle SteamVR
            SessionController.StartVRSession(wrapperType);

            //Wait for Vive to start
            if (!WaitForVive().Result) return;

            //Begin monitoring the different processes
            WrapperMonitoringThread.initializeMonitoring(wrapperType);

            Task.Factory.StartNew(() =>
            {
                currentProcess = new Process();
                currentProcess.StartInfo.FileName = SessionController.steam;
                currentProcess.StartInfo.Arguments = launch_params + experience.ID;

                //Add any extra launch parameters
                if (experience.Parameters != null)
                {
                    //Include a space before added more
                    currentProcess.StartInfo.Arguments += $" {experience.Parameters}";
                }

                currentProcess.Start();

                FindCurrentProcess();
            });
        }

        /// <summary>
        /// Collect the name of the application from the Steam install directory, the executable name is what windows uses
        /// as the 'Image Name' and will not change unless the executable is changed which does not matter for this function.
        /// </summary>
        private void GetGameProcessName()
        {
            string fileLocation = "S:\\SteamLibrary\\steamapps\\appmanifest_" + SteamScripts.lastApp.ID + ".acf";
            if (!File.Exists(fileLocation))
            {
                fileLocation = "C:\\Program Files (x86)\\Steam\\steamapps\\appmanifest_" + SteamScripts.lastApp.ID + ".acf";
                if (!File.Exists(fileLocation))
                {
                    launchingExperience = false;
                    throw new FileNotFoundException("Error", fileLocation);
                }
            }

            foreach (string line in File.ReadLines(fileLocation))
            {
                if (line.StartsWith("\t\"name\""))
                {
                    experienceName = line.Split("\t")[3].Trim('\"');
                }

                if (experienceName != null)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Wait for Vive to be open and connected before going any further with the launcher sequence.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> WaitForVive()
        {
            //Wait for the Vive Check
            Logger.WriteLog("About to launch a steam app, vive status is: " + WrapperMonitoringThread.viveStatus, MockConsole.LogLevel.Normal);
            if (launchingExperience)
            {
                SessionController.PassStationMessage("MessageToAndroid,AlreadyLaunchingGame");
                return false;
            }
            launchingExperience = true;

            if (!await ViveScripts.viveCheck(wrapperType))
            {
                launchingExperience = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Find the active process that has been launched.
        /// </summary>
        private void FindCurrentProcess()
        {
            int attempts = 0; //Track the loop for finding child processes

            Process? child = GetExperienceProcess();
            while(child == null && attempts < 10)
            {
                attempts++;
                MockConsole.WriteLine($"Checking for child process...", MockConsole.LogLevel.Debug);
                Task.Delay(3000).Wait();
                child = GetExperienceProcess();
            }
            currentProcess = child;
            launchingExperience = false;

            if(child != null)
            {
                SteamScripts.popupDetect = false;
                ListenForClose();
                SessionController.PassStationMessage($"ApplicationUpdate,{experienceName}/{currentProcess?.Id}/Steam");
            } else
            {
                UIUpdater.ResetUIDisplay();
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{experienceName}");
            }
        }

        /// <summary>
        /// Scan the active processes that to find the launched application. The process is matched by the 
        /// MainWindowTitle that is represented by the Steam application name.
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

        public void ListenForClose()
        {
            Task.Factory.StartNew(() =>
            {
                currentProcess?.WaitForExit();
                SteamScripts.popupDetect = false;
                SessionController.PassStationMessage($"ApplicationClosed");
                UIUpdater.ResetUIDisplay();
                WrapperManager.RecycleWrapper();
            });
        }

        public bool? CheckCurrentProcess()
        {
            return currentProcess?.Responding;
        }

        public void StopCurrentProcess()
        {
            if (currentProcess != null)
            {
                currentProcess.Kill();
            }
            ViveScripts.stopMonitoring();
            SteamScripts.popupDetect = false;
        }

        public void RestartCurrentProcess()
        {
            if(currentProcess != null)
            {
                currentProcess.Kill(true);
                Task.Delay(3000).Wait();
                WrapProcess(SteamScripts.lastApp);
            }
            SteamScripts.popupDetect = false;
        }

        public async void RestartCurrentSession()
        {
            StopCurrentProcess();

            List<string> combinedProcesses = new List<string>();
            combinedProcesses.AddRange(WrapperMonitoringThread.steamProcesses);
            combinedProcesses.AddRange(WrapperMonitoringThread.viveProcesses);

            CommandLine.queryVRProcesses(combinedProcesses, true);
            await SessionController.PutTaskDelay(2000);

            //have to add a waiting time to make sure it has exited
            int attempts = 0;

            if (SessionController.vrHeadset == null)
            {
                SessionController.PassStationMessage("No headset type specified.");
                SessionController.PassStationMessage("Processing,false");
                return;
            }

            List<string> processesToQuery = SessionController.vrHeadset.GetProcessesToQuery();
            while (CommandLine.queryVRProcesses(processesToQuery))
            {
                await SessionController.PutTaskDelay(1000); //blocks progress but does not stop the program
                if (attempts > 20)
                {
                    SessionController.PassStationMessage("MessageToAndroid,FailedRestart");

                    launchingExperience = false;

                    SessionController.PassStationMessage("Processing,false");
                    return;
                }
                attempts++;
            }

            await SessionController.PutTaskDelay(5000); //blocks progress but does not stop the program

            SessionController.StartVRSession(wrapperType);

            launchingExperience = false;

            SessionController.PassStationMessage("Processing,false");

            SessionController.PassStationMessage("MessageToAndroid,SetValue:session:Restarted");
        }
    }
}
