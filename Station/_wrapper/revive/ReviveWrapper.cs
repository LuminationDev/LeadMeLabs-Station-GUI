using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Station._utils;

namespace Station
{
    public class ReviveWrapper : Wrapper
    {
        public static string wrapperType = "Revive";
        private static Process? currentProcess;
        private static string launch_params = "-noreactlogin -login " + 
            Environment.GetEnvironmentVariable("SteamUserName", EnvironmentVariableTarget.Process) + " " + 
            Environment.GetEnvironmentVariable("SteamPassword", EnvironmentVariableTarget.Process) + " steam://rungameid/";
        public static string? experienceName = null;
        private static Experience lastExperience;
        private bool launchWillHaveFailedFromOpenVrTimeout = true;

        /// <summary>
        /// Track if an experience is being launched.
        /// </summary>
        public static bool launchingExperience = false;

        public Experience? GetLastExperience()
        {
            return lastExperience;
        }
        
        public void SetLastExperience(Experience experience)
        {
            lastExperience = experience;
        }

        public bool GetLaunchingExperience()
        {
            return launchingExperience;
        }

        public void SetLaunchingExperience(bool isLaunching)
        {
            launchingExperience = isLaunching;
        }
        
        public bool LaunchFailedFromOpenVrTimeout()
        {
            return launchWillHaveFailedFromOpenVrTimeout;
        }

        public string? GetCurrentExperienceName()
        {
            return experienceName;
        }

        public List<string>? CollectApplications()
        {
            return ReviveScripts.LoadAvailableGames();
        }

        public void CollectHeaderImage(string experienceName)
        {
            //TODO this is not working just yet
            Task.Factory.StartNew(() =>
            {
                //TODO change this as it relies on the .Contains method in ManifestReader
                string filePath = ManifestReader.GetApplicationImagePathByAppKey(ReviveScripts._reviveManifest, experienceName);

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
            throw new NotImplementedException();
        }
        
        public void SetCurrentProcess(Process process)
        {
            if (currentProcess != null)
            {
                currentProcess.Kill(true);
            }

            launchWillHaveFailedFromOpenVrTimeout = false;
            currentProcess = process;
            ListenForClose();
        }

        public string WrapProcess(Experience experience)
        {
            Logger.WriteLog($"WRAPPROCESS: Attempting to launch: {experience.Name}", MockConsole.LogLevel.Normal);
            
            launchWillHaveFailedFromOpenVrTimeout = false;
            if (experience.ID == null)
            {
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Unknown experience");
                return $"MessageToAndroid,GameLaunchFailed:Unknown experience";
            };

            if (SessionController.VrHeadset == null)
            {
                SessionController.PassStationMessage("No VR headset set.");
                return "No VR headset set.";
            }

            lastExperience = experience;
            GetGameProcessDetails();

            if(experienceName == null)
            {
                SessionController.PassStationMessage($"MessageToAndroid,GameLaunchFailed:Fail to find experience");
                Logger.WriteLog($"Unable to find Revive experience details (name) for: {experience.Name}", MockConsole.LogLevel.Normal);
                return $"Unable to find Revive experience details (name & install directory) for: {experience.Name}";
            }

            MockConsole.WriteLine($"Wrapping: {experienceName}", MockConsole.LogLevel.Debug);

            //Start the external processes to handle SteamVR
            SessionController.StartVRSession(wrapperType);

            //Begin monitoring the different processes
            WrapperMonitoringThread.InitializeMonitoring(wrapperType); //TODO finish this for REVIVE

            //Wait for the Headset's connection method to respond
            if (!SessionController.VrHeadset.WaitForConnection(wrapperType)) return "Could not connect to headset";

            //If headset management software is open (with headset connected) and OpenVrSystem cannot initialise then restart SteamVR
            if (!OpenVRManager.WaitForOpenVR().Result) return "Could not start OpenVR";

            Task.Factory.StartNew(() =>
            {
                //Attempt to start the process using OpenVR
                launchWillHaveFailedFromOpenVrTimeout = true;
                if (OpenVRManager.LaunchApplication(experience.ID))
                {
                    Logger.WriteLog($"SteamWrapper.WrapProcess: Launching {experience.Name} via OpenVR", MockConsole.LogLevel.Verbose);
                    return;
                }

                launchWillHaveFailedFromOpenVrTimeout = false;
            });
            return "launching";
        }
        
        /// <summary>
        /// Collect the name of the application from the Steam install directory, the executable name is what windows uses
        /// as the 'Image Name' and will not change unless the executable is changed which does not matter for this function.
        /// </summary>
        private void GetGameProcessDetails()
        {
            //IF error finding revive revive.manifest - bail out early
            if (!File.Exists(ReviveScripts._reviveManifest))
            {
                launchingExperience = false;
                throw new FileNotFoundException("Error", ReviveScripts._reviveManifest);
            }

            //LOOK for experience name in the revive.manifest file
            experienceName = ManifestReader.GetApplicationNameByAppKey(ReviveScripts._reviveManifest, lastExperience.ID);
        }

        public void ListenForClose()
        {
            Task.Factory.StartNew(() =>
            {
                currentProcess?.WaitForExit();
                experienceName = null; //Reset for correct headset state
                SteamScripts.popupDetect = false;
                SessionController.PassStationMessage($"ApplicationClosed");
                UIUpdater.ResetUIDisplay();
            });
        }

        public bool? CheckCurrentProcess()
        {
            return currentProcess?.Responding;
        }
        
        public bool HasCurrentProcess()
        {
            return currentProcess != null;
        }

        public void StopCurrentProcess()
        {
            if (currentProcess != null)
            {
                try
                {
                    currentProcess.Kill(true);
                }
                catch (InvalidOperationException e)
                {
                    
                }
            }
            //TODO update this for REVIVE
            CommandLine.StartProgram(SessionController.Steam, " +app_stop " + lastExperience.ID);
            SetLaunchingExperience(false);

            experienceName = null; //Reset for correct headset state
            WrapperMonitoringThread.StopMonitoring();
            ViveScripts.StopMonitoring();
            SteamScripts.popupDetect = false;
        }

        public void RestartCurrentExperience()
        {
            if(currentProcess != null)
            {
                StopCurrentProcess();
                Task.Delay(3000).Wait();
                WrapProcess(lastExperience);
            }
            SteamScripts.popupDetect = false;
        }

        /// <summary>
        /// Launch SteamVR as a process, SteamVR's appID is (250820)
        /// </summary>
        public static void LauncherSteamVR()
        {
            currentProcess = new Process();
            currentProcess.StartInfo.FileName = SessionController.Steam;
            currentProcess.StartInfo.Arguments = launch_params + 250820;
            currentProcess.Start();
        }
    }
}
