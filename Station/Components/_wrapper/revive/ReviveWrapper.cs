using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._models;
using Station.Components._monitoring;
using Station.Components._network;
using Station.Components._notification;
using Station.Components._openvr;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Components._wrapper.steam;
using Station.Components._wrapper.vive;
using Station.MVC.Controller;

namespace Station.Components._wrapper.revive;

public class ReviveWrapper : IWrapper
{
    public const string WrapperType = "Revive";
    private static Process? currentProcess;
    private static string? experienceName = null;
    private static Experience lastExperience;
    private bool _launchWillHaveFailedFromOpenVrTimeout = true;

    /// <summary>
    /// Track if an experience is being launched.
    /// </summary>
    private static bool launchingExperience = false;

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
        return _launchWillHaveFailedFromOpenVrTimeout;
    }

    public string? GetCurrentExperienceName()
    {
        return experienceName;
    }
    
    public List<T>? CollectApplications<T>()
    {
        return ReviveScripts.LoadAvailableExperiences<T>();
    }

    public void CollectHeaderImage(string experienceKey)
    {
        //TODO finish this off
        Task.Factory.StartNew(() =>
        {
            string? filePath = ManifestReader.GetApplicationImagePathByAppKey(ReviveScripts.ReviveManifest, experienceKey);

            if (!File.Exists(filePath))
            {
                MockConsole.WriteLine($"File not found:{filePath}", Enums.LogLevel.Error);
                
                JObject message = new JObject
                {
                    { "action", "StationError" },
                    { "value", $"File not found:{filePath}" }
                };
                SessionController.PassStationMessage(message);
                MessageController.SendResponse("Android", "Station", $"ThumbnailError:{experienceKey}");
                return;
            }

            //Add the header image to the sending image queue through action transformation
            SocketFile socketImage = new("experienceThumbnail", experienceKey, filePath);
            System.Action sendImage = new(() => socketImage.Send());

            //Queue the send function for invoking
            TaskQueue.Queue(false, sendImage);

            MockConsole.WriteLine($"Thumbnail for experience: {experienceKey} now queued for transfer.", Enums.LogLevel.Normal);
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

        _launchWillHaveFailedFromOpenVrTimeout = false;
        currentProcess = process;
        ListenForClose();
    }

    public string WrapProcess(Experience experience)
    {
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        
        _launchWillHaveFailedFromOpenVrTimeout = false;
        if (experience.ID == null)
        {
            JObject message = new JObject
            {
                { "action", "MessageToAndroid" },
                { "value", "GameLaunchFailed:Unknown experience" }
            };
            SessionController.PassStationMessage(message);
            return $"MessageToAndroid,GameLaunchFailed:Unknown experience";
        }

        if (vrProfile?.VrHeadset == null)
        {
            Logger.WriteLog("ReviveWrapper - WrapProcess: No VR headset set.", Enums.LogLevel.Error);
            return "No VR headset set.";
        }

        lastExperience = experience;
        GetGameProcessDetails();

        if(experienceName == null)
        {
            JObject message = new JObject
            {
                { "action", "MessageToAndroid" },
                { "value", "GameLaunchFailed:Fail to find experience" }
            };
            SessionController.PassStationMessage(message);
            Logger.WriteLog($"Unable to find Revive experience details (name) for: {experience.Name}", Enums.LogLevel.Normal);
            return $"Unable to find Revive experience details (name & install directory) for: {experience.Name}";
        }

        MockConsole.WriteLine($"Wrapping: {experienceName}", Enums.LogLevel.Debug);

        //Start the external processes to handle SteamVR
        SessionController.StartSession(WrapperType);

        //Begin monitoring the different processes
        WrapperMonitoringThread.InitializeMonitoring(WrapperType, experience.IsVr);

        if (InternalDebugger.GetHeadsetRequired())
        {
            //Wait for the Headset's connection method to respond
            if (!vrProfile.WaitForConnection(WrapperType))
            {
                lastExperience.Name = null; //Reset for correct headset state
                return "Could not get headset connection";
            }

            //If headset management software is open (with headset connected) and OpenVrSystem cannot initialise then restart SteamVR
            if (!OpenVrManager.WaitForOpenVr().Result)
            {
                lastExperience.Name = null; //Reset for correct headset state
                return "Could not connect to OpenVR";
            }
        }

        Task.Factory.StartNew(() =>
        {
            //Attempt to start the process using OpenVR
            _launchWillHaveFailedFromOpenVrTimeout = true;
            if (OpenVrManager.LaunchApplication(experience.ID))
            {
                Logger.WriteLog($"SteamWrapper.WrapProcess: Launching {experience.Name} via OpenVR", Enums.LogLevel.Verbose);
                return;
            }

            _launchWillHaveFailedFromOpenVrTimeout = false;
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
        if (!File.Exists(ReviveScripts.ReviveManifest))
        {
            launchingExperience = false;
            throw new FileNotFoundException("Error", ReviveScripts.ReviveManifest);
        }

        //LOOK for experience name in the revive.manifest file
        experienceName = ManifestReader.GetApplicationNameByAppKey(ReviveScripts.ReviveManifest, lastExperience.ID);
    }

    public void ListenForClose()
    {
        Task.Factory.StartNew(() =>
        {
            currentProcess?.WaitForExit();
            experienceName = null; //Reset for correct headset state
            SteamScripts.popupDetect = false;
            
            JObject message = new JObject
            {
                { "action", "ApplicationClosed" }
            };
            SessionController.PassStationMessage(message);
            UiUpdater.ResetUiDisplay();
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
                Logger.WriteLog($"StopCurrentProcess - ERROR: {e}", Enums.LogLevel.Error);
            }
        }

        StationCommandLine.StartProgram(SessionController.Steam, " +app_stop " + lastExperience.ID);
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
}
