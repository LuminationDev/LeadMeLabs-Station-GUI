using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._interfaces;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Components._wrapper.custom;
using Station.Components._wrapper.embedded;
using Station.Components._wrapper.revive;
using Station.Components._wrapper.steam;
using Station.MVC.Controller;
using Station.MVC.View;
using Valve.VR;

namespace Station.Components._openvr;

/// <summary>
/// Main functions and when they are called is detailed below:
/// InitialiseOpenVR
///     - Called in the StationMonitoringThread, continuously tries to initialise if it has not been already.
/// LoadManifests
///     - Called in the InitialiseOpenVR when OpenVR first establishes a connection.
/// WaitForOpenVR
///     - Called by a wrapper before launching an experience. Attempts to initialise OpenVR, if Vive is connected but OpenVR fails it restarts SteamVR and monitors for a new connection
/// QueryCurrentApplication
///     - Called in the StationMonitoringThread, continuously queries if there are any applications running in SteamVR, only if InitialiseOpenVR returns true
/// PerformDeviceChecks
///     - Called in the WrapperMonitoringThread, only if InitialiseOpenVR returns true (Checks the Headset, Controllers & Boundary)
/// OnVREvent
///     - Called in a constant loop in a parallel task after initialisation, this polls the OpenVR event with the soul purpose of detecting if SteamVR is closing and handles it gently 
/// </summary>
public class OpenVrManager
{
    public OpenVrSystem? openVrSystem;

    //Read this when launching an experience to know if it is VR (missing means it is standard)
    private static Dictionary<string, string>? vrApplicationDictionary;
    private bool _initialising;
    private CVRSystem? _ovrSystem;
    private bool _tracking;
    private uint _processId;
    private bool _quiting;
    private bool _deviceCheckInitialised;

    /// <summary>
    /// Create/instantiate the VRApplicationDictionary and load in the steam/custom vrmanifests
    /// </summary>
    public OpenVrManager()
    {
        vrApplicationDictionary = new Dictionary<string, string>();
    }

    #region Initialisation
    /// <summary>
    /// Initializes the OpenVR system and prepares it for interaction with VR hardware and applications.
    /// This function creates an OpenVRSystem instance, specifies the application type, and checks for
    /// successful initialisation. It stores the OpenVR system and initializes the _ovrSystem field.
    /// </summary>
    /// <returns>True if OpenVR initialisation is successful, otherwise false.</returns>
    public bool InitialiseOpenVr()
    {
        //OpenVR is already initialised and running
        if (openVrSystem is { OVRSystem: not null })
        {
            MockConsole.WriteLine("OpenVRSystem.OVRSystem initialised.", MockConsole.LogLevel.Verbose);
            return true;
        }
        
        //Do not double up. new OpenVRSystem takes time to recognise OpenVR status/connection.
        if (_initialising)
        {
            MockConsole.WriteLine("OpenVRSystem.OVRSystem already initialising.", MockConsole.LogLevel.Verbose);
            return false;
        }
        _initialising = true;

        MockConsole.WriteLine("Attempting to initialise OpenVRSystem.OVRSystem", MockConsole.LogLevel.Debug);

        //This requires SteamVR being open and running (WITH A HEADSET CONNECTED).
        openVrSystem = new OpenVrSystem(OpenVrSystem.ApplicationType.Other);

        if (openVrSystem.OVRSystem == null)
        {
            MockConsole.WriteLine("OpenVRSystem.OVRSystem has not been initialised.", MockConsole.LogLevel.Debug);
            _initialising = false;
            return false;
        }

        _ovrSystem = openVrSystem.OVRSystem;
        _quiting = false;
        Logger.WriteLog("OpenVRSystem.OVRSystem has been initialised.", MockConsole.LogLevel.Debug);
        
        try
        {
            //Load in the manifests as soon as a connection is established
            LoadManifests();
        }
        catch (Exception e)
        {
            Logger.WriteLog($"OnVREvent.LoadManifests task Error: {e}", MockConsole.LogLevel.Error);
        }
        
        //Create a listener for VR events - this handles the gentle exit of SteamVR
        new Task(OnVREvent).Start();

        UiUpdater.LoadImageFromAssetFolder("OpenVR", true);

        _initialising = false;
        return true;
    }

    /// <summary>
    /// Reset and load the VR manifests associated with OpenVR.
    /// This should be triggered at the start of the Station software.
    /// </summary>
    private void LoadManifests()
    {
        if (OpenVR.Applications == null)
        {
            MockConsole.WriteLine($"Cannot load manifests when SteamVR isn't open.", MockConsole.LogLevel.Normal);
            return;
        }

        // Force reset of the Steam & Custom VR manifest lists for guaranteed up to date. 
        OpenVR.Applications.RemoveApplicationManifest(CustomScripts.CustomManifest);
        OpenVR.Applications.RemoveApplicationManifest(EmbeddedScripts.EmbeddedVrManifest);
        OpenVR.Applications.RemoveApplicationManifest(SteamScripts.SteamManifest);
        OpenVR.Applications.RemoveApplicationManifest(ReviveScripts.ReviveManifest);
        OpenVR.Applications.AddApplicationManifest(CustomScripts.CustomManifest, true);
        OpenVR.Applications.AddApplicationManifest(EmbeddedScripts.EmbeddedVrManifest, true);
        OpenVR.Applications.AddApplicationManifest(SteamScripts.SteamManifest, true);
        OpenVR.Applications.AddApplicationManifest(ReviveScripts.ReviveManifest, true);

        // Load in the steam & custom manifest
        LoadVrManifest();
    }

    /// <summary>
    /// Wait for OpenVR to be open and connected before going any further with a wrappers' launcher sequence. No experience should
    /// be open at this point so there is no concern that killing SteamVR will exit and experience.
    /// </summary>
    /// <returns></returns>
    public static async Task<bool> WaitForOpenVr()
    {
        // Safe cast and null checks
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return false;

        MockConsole.WriteLine($"WaitForOpenVR - Checking SteamVR. Vive status: {Enum.GetName(typeof(DeviceStatus), vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus())} " +
            $"- OpenVR status: {MainController.openVrManager?.InitialiseOpenVr() ?? false}", MockConsole.LogLevel.Normal);

        //If Vive is connect but OpenVR is not/cannot be initialised, restart SteamVR and check again.
        if (vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus() == DeviceStatus.Connected && (!MainController.openVrManager?.InitialiseOpenVr() ?? true))
        {
            Logger.WriteLog($"OpenVRManager.WaitForOpenVR - Vive status: {vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus()}, " +
                $"OpenVR connection not established - restarting SteamVR", MockConsole.LogLevel.Normal);

            //Send message to the tablet (Updating what is happening)
            JObject message = new JObject
            {
                { "action", "SoftwareState" },
                { "value", "Restarting SteamVR" }
            };
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(1));

            //Kill SteamVR
            CommandLine.QueryProcesses(new List<string> { "vrmonitor" }, true);
            await Task.Delay(5000);

            //Launch SteamVR
            SteamWrapper.LaunchSteamVR();
            await Task.Delay(3000);

            bool steamvr = await Helper.MonitorLoop(() => ProcessManager.GetProcessesByName("vrmonitor").Length == 0, 10);
            if (!steamvr)
            {
                // Connection bailed out, send a failure message
                JObject androidMessage = new JObject
                {
                    { "action", "MessageToAndroid" },
                    { "value", "HeadsetTimeout" }
                };
                SessionController.PassStationMessage(androidMessage);
                return false;
            }

            Logger.WriteLog($"OpenVRManager.WaitForOpenVR - Vive status: {vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus()}, " +
                $"SteamVR restarted successfully", MockConsole.LogLevel.Normal);

            //Send message to the tablet (Updating what is happening)
            JObject stateMessage = new JObject
            {
                { "action", "SoftwareState" },
                { "value", "Connecting SteamVR" }
            };
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(stateMessage), TimeSpan.FromSeconds(1));

            bool openvr = await Helper.MonitorLoop(() => !MainController.openVrManager?.InitialiseOpenVr() ?? true, 10);
            if (!openvr)
            {
                JObject errorMessage = new JObject
                {
                    { "action", "SoftwareState" },
                    { "value", "SteamVR Error" }
                };
                ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(errorMessage), TimeSpan.FromSeconds(1));
                return false;
            }

            Logger.WriteLog($"OpenVRManager.WaitForOpenVR - Vive status: {vrProfile.VrHeadset.GetHeadsetManagementSoftwareStatus()}, " +
                $"OpenVR connection established", MockConsole.LogLevel.Normal);
        }

        return true;
    }
    #endregion

    #region OpenVR Events
    /// <summary>
    /// Handles VR events by continuously polling the OpenVR system for events. 
    /// When a VR event is received, it is processed based on its type.
    /// If the event indicates the VR application is quitting, necessary actions are taken,
    /// such as acknowledging the quit request and shutting down the OpenVR system.
    /// </summary>
    private void OnVREvent()
    {
        VREvent_t vrEvent = new VREvent_t();

        while (!_quiting)
        {
            if (_ovrSystem == null || !_ovrSystem.PollNextEvent(ref vrEvent,
                    (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t)))) continue;
            
            //TODO work out what to do if the program (SteamVR) has quit.

            // Handle the specific VR event
            switch((EVREventType)vrEvent.eventType)
            {
                case EVREventType.VREvent_RestartRequested:
                    //TODO send a message to the nuc?/Handle restart?
                    //IDEA: SteamVR is in the _steamManifest, check if launching it closes the current steamvr program and opens a new one? Essentially a restart
                    Logger.WriteLog("OpenVRManager.OnVREvent - SteamVR requires a restart", MockConsole.LogLevel.Normal);
                    break;

                case EVREventType.VREvent_Quit:
                    _quiting = true;
                    Logger.WriteLog("SteamVR quitting", MockConsole.LogLevel.Normal);
                    _ovrSystem.AcknowledgeQuit_Exiting();
                    openVrSystem?.Shutdown();
                    openVrSystem = null;
                    UiUpdater.LoadImageFromAssetFolder("OpenVR", false);
                    break;
            }

            vrEvent = new VREvent_t();
        }
    }
    #endregion

    #region OpenVR Applications
    /// <summary>
    /// Loads and processes information from the VR manifests of applications registered with OpenVR.
    /// This function retrieves details about VR applications registered in OpenVR, filters and extracts
    /// relevant application information, and populates the ApplicationDictionary with VR application entries.
    /// The method collects application keys, names, types, and other properties to determine the VR experience.
    /// </summary>
    private void LoadVrManifest()
    {
        int vrApplicationCount = 0;

        uint applicationCount = OpenVR.Applications.GetApplicationCount();
        for (uint index = 0; index < applicationCount; index++)
        {
            StringBuilder pchKeyBuffer = new StringBuilder(256);
            uint bufferSize = (uint)pchKeyBuffer.Capacity;
            EVRApplicationError error =
                OpenVR.Applications.GetApplicationKeyByIndex(index, pchKeyBuffer, bufferSize);

            if (error == EVRApplicationError.None)
            {
                string pchKey = pchKeyBuffer.ToString();
                if (!pchKey.Contains("steam.app") && !pchKey.Contains("custom.app") && !pchKey.Contains("revive.app") &&
                    !pchKey.Contains("embedded.app")) continue;
                
                // Get the application properties using the pch key
                // string applicationName =
                //     GetApplicationPropertyString(pchKey, EVRApplicationProperty.Name_String);
                // string applicationLaunchType =
                //     GetApplicationPropertyString(pchKey, EVRApplicationProperty.LaunchType_String);
                    
                // string output = $"Application Key: {pchKey} " +
                //                 $"Application Name: {applicationName} " +
                //                 $"Application Index: {index} " +
                //                 $"Application Type: {applicationLaunchType}";
                    
                //Logger.WriteLog(output, MockConsole.LogLevel.Normal);

                vrApplicationCount++;

                //Get the application ID
                string appId;
                if(pchKey.Contains("steam.app"))
                {
                    appId = pchKey.Replace("steam.app.", "");
                } 
                else if (pchKey.Contains("custom.app"))
                {
                    appId = pchKey.Replace("custom.app.", "");
                } 
                else if (pchKey.Contains("revive.app"))
                {
                    appId = pchKey.Replace("revive.app.", "");
                }
                else if (pchKey.Contains("embedded.app"))
                {
                    appId = pchKey.Replace("embedded.app.", "");
                }
                else
                {
                    continue;
                }

                //If an application is in the dictionary it is therefore a VR experience
                if (!vrApplicationDictionary?.ContainsKey(appId) ?? false)
                {
                    vrApplicationDictionary.Add(appId, pchKey);
                }
            }
            else
            {
                MockConsole.WriteLine($"Failed to get the Steam pch key. Error: {error}", MockConsole.LogLevel.Debug);
            }
        }

        Logger.WriteLog($"OpenVRManager.LoadVrManifest: VR application count: {vrApplicationCount}", MockConsole.LogLevel.Verbose);
    }

    /// <summary>
    /// Attempt to find the experience name in the ApplicationDictionary, if present this means the experience is
    /// VR, if not then it must be a regular experience.
    /// </summary>
    /// <param name="appId">A string of the experience's application ID to load</param>
    /// <returns>A bool if the experience is VR</returns>
    public static bool LaunchApplication(string appId)
    {
        if (vrApplicationDictionary == null) return false;

        vrApplicationDictionary.TryGetValue(appId, out var pchKey);
        if (pchKey == null)
        {
            Logger.WriteLog($"OPENVR: {appId} has no pchKey. Make sure OpenVR has been initialised and " +
                            $"manifests have been loaded", MockConsole.LogLevel.Normal);
            return false;
        }
        
        EVRApplicationError error = OpenVR.Applications.LaunchApplication(pchKey);
        if (error == EVRApplicationError.None)
        {
            ScheduledTaskQueue.EnqueueTask(() =>
            {
                if (!(WrapperManager.currentWrapper?.LaunchFailedFromOpenVrTimeout() ?? false)) return;
                
                WrapperManager.currentWrapper.StopCurrentProcess();
                UiUpdater.ResetUiDisplay();
                
                JObject message = new JObject
                {
                    { "action", "MessageToAndroid" },
                    { "value", $"GameLaunchFailed:{WrapperManager.currentWrapper.GetLastExperience()?.Name}" }
                };
                SessionController.PassStationMessage(message);
            
                JObject response = new JObject { { "response", "ExperienceLaunchFailed" } };
                JObject responseData = new JObject { { "experienceId", WrapperManager.currentWrapper.GetLastExperience()?.ID } };
                response.Add("responseData", responseData);
            
                MessageController.SendResponse("NUC", "QA", response.ToString());
            }, TimeSpan.FromSeconds(30));
            
            // Check if there are any confirmation windows associated with the experience
            WrapperManager.PerformExperienceWindowConfirmations();
        }
        return error == EVRApplicationError.None;
    }

    /// <summary>
    /// Queries information about the currently running application in the SteamVR runtime environment using OpenVR SDK.
    /// Retrieves the ID, name, and status of the application and updates the relevant information in the 'App' object.
    /// </summary>
    public void QueryCurrentApplication()
    {
        CVRApplications applications = OpenVR.Applications;
        uint queriedProcessId = applications.GetCurrentSceneProcessId();
        
        //If _processId is 0 there is no active process, if _queriedProcessId is different then the application has changed
        if (queriedProcessId == 0 || queriedProcessId == _processId) return;
        _processId = queriedProcessId;
            
        //Gets the active application pchKey running on SteamVR
        StringBuilder appKeyBuffer = new StringBuilder(256); // Adjust the buffer size as needed

        EVRApplicationError error =
            applications.GetApplicationKeyByProcessId(_processId, appKeyBuffer, (uint)appKeyBuffer.Capacity);

        if (error != EVRApplicationError.None)
        {
            MockConsole.WriteLine($"Failed to get the current application key. Error: {error}", MockConsole.LogLevel.Debug);
            return;
        }

        string currentAppKey = appKeyBuffer.ToString();
        string currentAppType = appKeyBuffer.ToString().Split(".")[0]; 
        currentAppType = currentAppType.Substring(0, 1).ToUpper() + currentAppType.Substring(1);
            
            
        // Retrieve the name of the application using the application key
        StringBuilder appNameBuffer = new StringBuilder(256); // Adjust the buffer size as needed
        EVRApplicationError
            getAppNameError = EVRApplicationError.None; // Additional parameter for error handling

        applications.GetApplicationPropertyString(
            currentAppKey,
            EVRApplicationProperty.Name_String,
            appNameBuffer,
            (uint)appNameBuffer.Capacity,
            ref getAppNameError);

        if (getAppNameError != EVRApplicationError.None)
        {
            Logger.WriteLog($"OpenVRManager.QueryCurrentApplication - Failed to get the application name. Error: {getAppNameError}", 
                MockConsole.LogLevel.Debug);
            return;
        }

        string currentAppName = appNameBuffer.ToString();
        // string? currentAppStatus = Enum.GetName(typeof(EVRSceneApplicationState),
        //     applications.GetSceneApplicationState());
        //
        // string output = $"Application ID: {_processId}\n" +
        //                 $"Application State: {currentAppStatus}\n" +
        //                 $"Application Type: {currentAppType}\n" +
        //                 $"Currently Running Application Key: {currentAppKey}\n" +
        //                 $"Currently Running Application Name: {currentAppName}";
        //
        // Logger.WriteLog(output, MockConsole.LogLevel.Verbose);

        // Get the process associated with the _appId
        Process? targetProcess = ProcessManager.GetProcessById((int)_processId);
        if (targetProcess == null)
        {
            Logger.WriteLog($"OpenVRManager.QueryCurrentApplication - Target Process NOT found.",
                MockConsole.LogLevel.Normal);
            _processId = 0;
            return;
        }

        WrapperManager.LoadWrapper(currentAppType); //Load in the appropriate wrapper type
        WrapperManager.ApplicationList.TryGetValue(currentAppKey.Split(".")[2], out var experience);
        WrapperManager.currentWrapper?.SetLastExperience(experience);
        WrapperManager.currentWrapper?.SetCurrentProcess(targetProcess); //Sets the wrapper process and calls WaitForExit
        WrapperManager.currentWrapper?.SetLaunchingExperience(false);
            
        WindowManager.MaximizeProcess(targetProcess); //Maximise the process experience

        string? experienceId = experience.ID;
        if (string.IsNullOrEmpty(experienceId))
        {
            experienceId = "0";
        }
            
        // Send a message to the NUC
        JObject experienceInformation = new JObject
        {
            { "name", currentAppName },
            { "appId", experienceId },
            { "wrapper", currentAppType }
        };
        
        JObject message = new JObject
        {
            { "action", "ApplicationUpdate" },
            { "info", experienceInformation }
        };
        SessionController.PassStationMessage(message);
            
        JObject response = new JObject();
        response.Add("response", "ExperienceLaunched");
        JObject responseData = new JObject();
        responseData.Add("experienceId", experienceId);
        response.Add("responseData", responseData);
            
        MessageController.SendResponse("NUC", "QA", response.ToString());

        // Update the Station UI
        UiUpdater.UpdateProcess(targetProcess.MainWindowTitle);
        UiUpdater.UpdateStatus("Running...");
    }
    #endregion

    #region OpenVR Devices
    /// <summary>
    /// Start a new task containing a loop tied to the OnVREvent quiting variable, only if the loop is not already running. 
    /// Check the connected VR devices, if _tracking is enabled then check if the boundary is configured.
    /// </summary>
    public void StartDeviceChecks()
    {
        if (_deviceCheckInitialised) return; //Do not double up on the check loop
        _deviceCheckInitialised = true;

        new Task(async () => {
            while (!_quiting)
            {
                //Run through all connected devices
                CheckDevices();

                // Boundary && Controller Information (only check if the headset is connected and tracking)
                if (_tracking) {
                    CheckBoundary();
                }
                
                //Minor delay - Test wait times
                await Task.Delay(1000);
            }
            _deviceCheckInitialised = false;
        }).Start();
    }

    /// <summary>
    /// Loop through the available devices.
    /// </summary>
    private void CheckDevices()
    {
        if (_ovrSystem == null)
        {
            return;
        }

        // Track the number of connected controllers & base stations
        int controllerCount = 0;

        for (uint deviceIndex = 0; deviceIndex < OpenVR.k_unMaxTrackedDeviceCount; deviceIndex++)
        {
            switch (_ovrSystem.GetTrackedDeviceClass(deviceIndex))
            {
                case ETrackedDeviceClass.HMD:
                    GetHeadsetPositionAndOrientation(deviceIndex);
                    break;
                case ETrackedDeviceClass.Controller:
                    controllerCount++;
                    GetControllerInfo(deviceIndex);
                    break;
                case ETrackedDeviceClass.TrackingReference:
                    GetBaseStationInfo(deviceIndex);
                    break;
            }
        }

        if (controllerCount != 0) return;
        MockConsole.WriteLine($"No controllers currently connected.", MockConsole.LogLevel.Debug);
    }

    #region Boundary Details
    /// <summary>
    /// Checks the calibration state of the VR Chaperone system using OpenVR SDK.
    /// If the Chaperone interface is available, it retrieves and outputs the calibration state to the console if in debug mode.
    /// </summary>
    private void CheckBoundary()
    {
        if (_ovrSystem == null)
        {
            return;
        }

        CVRChaperone chaperone = OpenVR.Chaperone;
        if (chaperone == null)
        {
            return;
        }
        MockConsole.WriteLine($"CVRChaperone CalibrationState: {chaperone.GetCalibrationState()}", MockConsole.LogLevel.Verbose);
    }

    /// <summary>
    /// Reload the boundary fence with the currently set collision bounds and play area within Steam's chaperone_info.vrchap.
    /// This instantly refreshes the boundary with having to exit/restart SteamVR.
    /// </summary>
    public void ReloadBoundary() //TODO this is not called anywhere yet
    {
        if (_ovrSystem == null)
        {
            return;
        }

        CVRChaperoneSetup chaperoneSetup = OpenVR.ChaperoneSetup;
        if (chaperoneSetup == null)
        {
            return;
        }

        chaperoneSetup.ReloadFromDisk(EChaperoneConfigFile.Live);
    }
    #endregion

    #region Headset Information
    /// <summary>
    /// Obtains the position and orientation of the VR headset (HMD) using OpenVR SDK.
    /// Updates the "App" object with the tracking status of the headset.
    /// Outputs headset tracking information to the console if debug mode is enabled.
    /// </summary>
    /// <param name="headsetIndex">The index of the tracked device to query.</param>
    private void GetHeadsetPositionAndOrientation(uint headsetIndex)
    {
        // Safe cast and null checks
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return;
        
        if (_ovrSystem == null)
        {
            return;
        }
        
        // Get the device pose information
        TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        ETrackingUniverseOrigin trackingOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;

        _ovrSystem.GetDeviceToAbsoluteTrackingPose(trackingOrigin, 0, poses);

        // Extract the headset's position and orientation (All 0's means it has lost tracking)
        HmdMatrix34_t poseMatrix = poses[headsetIndex].mDeviceToAbsoluteTracking;
        Vector3 headsetPosition = new Vector3(poseMatrix.m3, poseMatrix.m7, poseMatrix.m11);

        Quaternion headsetOrientation = new Quaternion(
            poseMatrix.m0, poseMatrix.m1, poseMatrix.m2, -poseMatrix.m3
        );

        // Check if the headset pose is valid and being tracked
        // string output = $"bDeviceIsConnected: {poses[headsetIndex].bDeviceIsConnected}\n" +
        //                 $"bPoseIsValid: {poses[headsetIndex].bPoseIsValid}\n" +
        //                 $"trackingStatus: {poses[headsetIndex].eTrackingResult}\n" +
        //                 $"Headset Position: {headsetPosition}\n" +
        //                 $"Headset Orientation: {headsetOrientation}";

        //Logger.WriteLog(output, MockConsole.LogLevel.Verbose);

        if (headsetPosition == new Vector3(0, 0, 0) && headsetOrientation == new Quaternion(1, 0, 0, 0))
        {
            _tracking = false;
            vrProfile.VrHeadset?.GetStatusManager().UpdateHeadset(VrManager.OpenVR, DeviceStatus.Lost);
            MockConsole.WriteLine("Headset lost", MockConsole.LogLevel.Debug);
        }
        else if (headsetPosition != new Vector3(0, 0, 0) && headsetOrientation != new Quaternion(1, 0, 0, 0))
        {
            _tracking = true;
            vrProfile.VrHeadset?.GetStatusManager().UpdateHeadset(VrManager.OpenVR, DeviceStatus.Connected);
            MockConsole.WriteLine("Headset found", MockConsole.LogLevel.Debug);
        }

        vrProfile.VrHeadset?.GetStatusManager().UpdateHeadsetFirmwareStatus(GetFirmwareUpdateRequired(headsetIndex));

        //Collect the headset model - only do this if it hasn't been set already.
        if (MainWindow.headsetDescription != null && 
            ((vrProfile.VrHeadset?.GetStatusManager().HeadsetDescription.Equals("") ?? true) || 
            (vrProfile.VrHeadset?.GetStatusManager().HeadsetDescription.Equals("Unknown") ?? true))
        )
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            var renderModelName = new StringBuilder();
            _ovrSystem.GetStringTrackedDeviceProperty(headsetIndex,
                ETrackedDeviceProperty.Prop_ModelNumber_String,
                renderModelName, OpenVR.k_unMaxPropertyStringSize, ref error);

            MockConsole.WriteLine($"Headset description: {renderModelName}",
                MockConsole.LogLevel.Verbose);

            if (error == ETrackedPropertyError.TrackedProp_Success)
            {
                vrProfile.VrHeadset?.GetStatusManager().SetHeadsetDescription(renderModelName.ToString());
                UiUpdater.UpdateOpenVrStatus("headsetDescription", renderModelName.ToString());
            }
        }
    }
    #endregion

    #region Controller Information
    /// <summary>
    /// Obtains information about the connected VR controllers using OpenVR SDK.
    /// Updates the "App" object with the information about the connected controllers, including their roles and battery levels.
    /// Outputs controller information to the console if debug mode is enabled.
    /// </summary>
    /// <param name="controllerIndex">The index of the tracked device to query.</param>
    private void GetControllerInfo(uint controllerIndex)
    {
        // Safe cast and null checks
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return;
        
        if (_ovrSystem == null)
        {
            return;
        }

        // Track any error messages
        ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;

        // Get the controller role (left or right)
        ETrackedControllerRole role = _ovrSystem.GetControllerRoleForTrackedDeviceIndex(controllerIndex);
        
        // Get the controller serial number
        var serialNumber = GetSerialNumber(controllerIndex);

        //Check the pose of the controller
        vrProfile.VrHeadset?.GetStatusManager().UpdateController(
            serialNumber, null, "tracking", IsDeviceConnected(controllerIndex) ? DeviceStatus.Connected : DeviceStatus.Lost);
        
        var firmwareUpdateRequired = GetFirmwareUpdateRequired(controllerIndex);
        vrProfile.VrHeadset?.GetStatusManager().UpdateController(
            serialNumber, null, "firmware_update_required", firmwareUpdateRequired);

        if (role == ETrackedControllerRole.Invalid) return;
        
        DeviceRole controllerRole = role == ETrackedControllerRole.LeftHand ? DeviceRole.Left : DeviceRole.Right;
        
        // Some headsets have specific controller roles baked into their serial numbers
        string lowerCaseSerial = serialNumber.ToLower();
        controllerRole = lowerCaseSerial switch
        {
            _ when lowerCaseSerial.Contains("right") => DeviceRole.Right,
            _ when lowerCaseSerial.Contains("left") => DeviceRole.Left,
            _ => controllerRole
        };

        // Get the controller battery percentage as a float value
        float batteryLevel = _ovrSystem.GetFloatTrackedDeviceProperty(controllerIndex,
            ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);

        // Check if the battery percentage is valid (error == TrackedProp_Success)
        if (error == ETrackedPropertyError.TrackedProp_Success) //does this mean the controller is lost?
        {
            int formattedBatteryLevel = (int)(batteryLevel * 100);

            MockConsole.WriteLine(
                $"Controller {controllerIndex} (Role: {Enum.GetName(typeof (DeviceRole), controllerRole)} - " +
                $"Serial Number: {serialNumber}, " +
                $"Battery Level: {formattedBatteryLevel}%", 
                MockConsole.LogLevel.Verbose);
            
            vrProfile.VrHeadset?.GetStatusManager().UpdateController(
                serialNumber, controllerRole, "battery", formattedBatteryLevel);
        }
        else
        {
            // Handle the case when battery level retrieval fails
            MockConsole.WriteLine(
                $"Failed to get the battery level for controller {controllerIndex}. Error: {error}", 
                MockConsole.LogLevel.Verbose);
        }
    }
    #endregion
    
    #region Base Station Information
    private void GetBaseStationInfo(uint baseStationIndex)
    {
        // Safe cast and null checks
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return;
        
        if (_ovrSystem == null)
        {
            return;
        }

        var serialNumber = GetSerialNumber(baseStationIndex);
        var isConnected = IsDeviceConnected(baseStationIndex);
        var firmwareUpdateRequired = GetFirmwareUpdateRequired(baseStationIndex);
        vrProfile.VrHeadset?.GetStatusManager().UpdateBaseStation(serialNumber, "tracking", isConnected ? DeviceStatus.Connected : DeviceStatus.Lost);
        vrProfile.VrHeadset?.GetStatusManager().UpdateBaseStation(serialNumber, "firmware_update_required", firmwareUpdateRequired);
    }
    #endregion
    
    #region Helpers
    /// <summary>
    /// Retrieves a string property associated with a VR application using its application key.
    /// This function queries the OpenVR system for a specific string property of a VR application,
    /// identified by the provided application key. If the property retrieval is successful, the
    /// retrieved string is returned; otherwise, "Unknown" is returned.
    /// </summary>
    /// <param name="pchKey">The application key identifying the VR application.</param>
    /// <param name="property">The property to retrieve as an EVRApplicationProperty.</param>
    /// <returns>The retrieved string property value or "Unknown" if retrieval failed.</returns>
    private string GetApplicationPropertyString(string pchKey, EVRApplicationProperty property)
    {
        StringBuilder sb = new StringBuilder(256);
        EVRApplicationError error = EVRApplicationError.None; // Additional parameter for error handling
        OpenVR.Applications.GetApplicationPropertyString(pchKey, property, sb, (uint)sb.Capacity, ref error);
        return error == EVRApplicationError.None ? sb.ToString() : "Unknown";
    }

    /// <summary>
    /// Retrieve the serial number (alpha-numeric) of a currently connected device.
    /// </summary>
    /// <param name="deviceIndex">A uint of the device's index retrieved from OpenVR</param>
    /// <returns>A string of the devices serial number</returns>
    private string GetSerialNumber(uint deviceIndex)
    {
        if (_ovrSystem == null)
        {
            return "Unknown";
        }
        
        StringBuilder serialNumberBuilder = new StringBuilder(256);

        ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
        _ovrSystem.GetStringTrackedDeviceProperty(deviceIndex,
            ETrackedDeviceProperty.Prop_SerialNumber_String, serialNumberBuilder,
            (uint)serialNumberBuilder.Capacity, ref error);
        string serialNumber = serialNumberBuilder.ToString();

        if (error == ETrackedPropertyError.TrackedProp_Success)
        {
            return serialNumber;
        }
        
        return "Unknown";
    }

    private bool GetFirmwareUpdateRequired(uint deviceIndex)
    {
        if (_ovrSystem == null)
        {
            return false;
        }

        ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
        bool result = _ovrSystem.GetBoolTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_Firmware_UpdateAvailable_Bool, ref error);

        if (error == ETrackedPropertyError.TrackedProp_Success)
        {
            return result;
        }
        
        return false;
    }

    /// <summary>
    /// Check the OVRSystem for a devices current pose and determine if that device is connected
    /// to the system at the moment.
    /// </summary>
    /// <param name="deviceIndex">A uint of the device's index retrieved from OpenVR</param>
    /// <returns>A bool of if the device is currently connected.</returns>
    private bool IsDeviceConnected(uint deviceIndex)
    {
        if (_ovrSystem == null)
        {
            return false;
        }
        
        // Get the device pose information
        TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        ETrackingUniverseOrigin trackingOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;

        _ovrSystem.GetDeviceToAbsoluteTrackingPose(trackingOrigin, 0, poses);

        // Extract the headset's position and orientation (All 0's means it has lost tracking)
        HmdMatrix34_t poseMatrix = poses[deviceIndex].mDeviceToAbsoluteTracking;
        Vector3 headsetPosition = new Vector3(poseMatrix.m3, poseMatrix.m7, poseMatrix.m11);

        Quaternion headsetOrientation = new Quaternion(
            poseMatrix.m0, poseMatrix.m1, poseMatrix.m2, -poseMatrix.m3
        );

        // Check if the headset pose is valid and being tracked
        string output = $"bDeviceIsConnected: {poses[deviceIndex].bDeviceIsConnected}\n" +
                        $"bPoseIsValid: {poses[deviceIndex].bPoseIsValid}\n" +
                        $"trackingStatus: {poses[deviceIndex].eTrackingResult}\n" +
                        $"Device Position: {headsetPosition}\n" +
                        $"Device Orientation: {headsetOrientation}";
        
        MockConsole.WriteLine(output, MockConsole.LogLevel.Verbose);
        return poses[deviceIndex].bDeviceIsConnected;
    }
    #endregion
    #endregion
}
