using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Station
{
    public class SteamWrapper : Wrapper
    {
        public static string wrapperType = "Steam";
        private static Process? currentProcess;
        private static string launch_params = "-noreactlogin -login " + Environment.GetEnvironmentVariable("SteamUserName") + " " + Environment.GetEnvironmentVariable("SteamPassword") + " steam://rungameid/";
        public static string? experienceName = null;
        public static string? installDir = null;

        /// <summary>
        /// Track if an experience is being launched.
        /// </summary>
        public static bool launchingExperience = false;

        public string? GetCurrentExperienceName()
        {
            return experienceName;
        }

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
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Unknown experience");
                return;
            };

            SteamScripts.lastApp = experience;
            GetGameProcessDetails();

            if(experienceName == null || installDir == null)
            {
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Fail to find experience");
                Logger.WriteLog($"Unable to find Steam experience details (name & install directory) for: {experience.Name}", MockConsole.LogLevel.Normal);
                return;
            }

            MockConsole.WriteLine($"Wrapping: {experienceName}", MockConsole.LogLevel.Debug);

            //Start the external processes to handle SteamVR
            SessionController.StartVRSession(wrapperType);

            //Begin monitoring the different processes
            WrapperMonitoringThread.initializeMonitoring(wrapperType);

            //Wait for Vive to start
            if (!WaitForVive().Result) return;

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
        private void GetGameProcessDetails()
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

            Logger.WriteLog($"Steam experience file location: {fileLocation}", MockConsole.LogLevel.Normal);

            experienceName = null;
            installDir = null;

            foreach (string line in File.ReadLines(fileLocation))
            {
                if (line.StartsWith("\t\"name\""))
                {
                    experienceName = line.Split("\t")[3].Trim('\"');
                }

                if (line.StartsWith("\t\"installdir\""))
                {
                    installDir = line.Split("\t")[3].Trim('\"');
                }

                if (experienceName != null && installDir != null)
                {
                    break;
                }
            }

            Logger.WriteLog($"Steam experience install directory: {installDir}", MockConsole.LogLevel.Normal);
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

            if (!await ViveScripts.ViveCheck(wrapperType))
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
                Logger.WriteLog($"Child process found: {child.Id}, {child.MainWindowTitle}, {child.ProcessName}", MockConsole.LogLevel.Normal);

                SteamScripts.popupDetect = false;
                ListenForClose();
                WindowManager.MaximizeProcess(child); //Maximise the process experience
                SessionController.PassStationMessage($"ApplicationUpdate,{experienceName}/{SteamScripts.lastApp.ID}/Steam");
            } else
            {
                Logger.WriteLog("Game launch failure: " + SteamScripts.lastApp.Name, MockConsole.LogLevel.Normal);
                UIUpdater.ResetUIDisplay();
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:{SteamScripts.lastApp.Name}");
            }
        }

        /// <summary>
        /// Scan the active processes that to find the launched application. The process is matched by the 
        /// MainWindowTitle that is represented by the Steam application name.
        /// </summary>
        /// <returns>The launched application process</returns>
        private Process? GetExperienceProcess()
        {
            if (installDir != null)
            {
                string? activeProcessId = null;
                string steamPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\" + installDir;
                string? processId = CommandLine.GetProcessIdFromDir(steamPath);
                if (processId != null)
                {
                    activeProcessId = processId;
                }

                steamPath = "S:\\SteamLibrary\\steamapps\\common\\" + installDir;
                processId = CommandLine.GetProcessIdFromDir(steamPath);
                if (processId != null)
                {
                    Logger.WriteLog("A proccess ID was found: " + processId, MockConsole.LogLevel.Normal);
                    activeProcessId = processId;
                }
                if (activeProcessId != null)
                {
                    Process proc = Process.GetProcessById(Int32.Parse(activeProcessId));
                    Logger.WriteLog($"Application found: {proc.MainWindowTitle}/{proc.Id}", MockConsole.LogLevel.Debug);

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
            ViveScripts.StopMonitoring();
            SteamScripts.popupDetect = false;
        }

        public void RestartCurrentExperience()
        {
            if(currentProcess != null)
            {
                currentProcess.Kill(true);
                Task.Delay(3000).Wait();
                WrapProcess(SteamScripts.lastApp);
            }
            SteamScripts.popupDetect = false;
        }

        public static void HandleOfflineSteam()
        {
            new Thread(() =>
            {
                OverlayManager.OverlayThreadManual("Initializing VR experiences ");
                Process? steamSignInWindow = null;
                Timer timer = new Timer(1000);
                int attempts = 0;

                void TimerElapsed(object? obj, ElapsedEventArgs args)
                {
                    if (attempts > 10)
                    {
                        timer.Stop();
                        OverlayManager.ManualStop();
                    }
                    List<Process> list = CommandLine.GetProcessesByName(new List<string> { "steam" });
                    foreach (Process process in list)
                    {
                        Logger.WriteLog($"Looking for steam sign in process: Process: {process.ProcessName} ID: {process.Id}, MainWindowTitle: {process.MainWindowTitle}", MockConsole.LogLevel.Debug);

                        if (process.MainWindowTitle.Equals("Steam Sign In"))
                        {
                            steamSignInWindow = process;
                            timer.Stop();
                            MockConsole.WriteLine($"Time for powershell command", MockConsole.LogLevel.Debug);
                            CommandLine.PowershellCommand(steamSignInWindow);
                        }
                    }

                    attempts++;
                }
                timer.Elapsed += TimerElapsed;
                timer.AutoReset = true;
                timer.Enabled = true;
            }).Start();
        }
    }
}
