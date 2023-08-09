using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace Station
{
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
    public class OpenVRManager
    {
        public OpenVRSystem? OpenVrSystem;
        
        private readonly string _steamManifest = @"C:\Program Files (x86)\Steam\config\steamapps.vrmanifest";
        private readonly string _customManifest = Path.GetFullPath(Path.Combine(CommandLine.stationLocation, @"..", "customapps.vrmanifest"));

        //Read this when launching an experience to know if it is VR (missing means it is standard)
        private static Dictionary<string, string>? _vrApplicationDictionary;
        private static bool _initialising = false;
        private CVRSystem? _ovrSystem;
        private bool _tracking;
        private uint _processId;

        /// <summary>
        /// Create/instantiate the VRApplicationDictionary and load in the steam/custom vrmanifests
        /// </summary>
        public OpenVRManager()
        {
            _vrApplicationDictionary = new Dictionary<string, string>();
        }

        #region Initialisation
        /// <summary>
        /// Initializes the OpenVR system and prepares it for interaction with VR hardware and applications.
        /// This function creates an OpenVRSystem instance, specifies the application type, and checks for
        /// successful initialisation. It stores the OpenVR system and initializes the _ovrSystem field.
        /// </summary>
        /// <returns>True if OpenVR initialisation is successful, otherwise false.</returns>
        public bool InitialiseOpenVR()
        {
            //OpenVR is already initialised and running
            if (OpenVrSystem != null && OpenVrSystem.OVRSystem != null)
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
            OpenVrSystem = new OpenVRSystem(OpenVRSystem.ApplicationType.Other);

            if (OpenVrSystem.OVRSystem == null)
            {
                MockConsole.WriteLine("OpenVRSystem.OVRSystem has not been initialised.", MockConsole.LogLevel.Debug);
                _initialising = false;
                return false;
            }

            _ovrSystem = OpenVrSystem.OVRSystem;
            Logger.WriteLog("OpenVRSystem.OVRSystem has been initialised.", MockConsole.LogLevel.Debug);
            
            try
            {
                //Load in the manifests as soon as a connection is established
                LoadManifests();
            }
            catch (Exception e)
            {
                MockConsole.WriteLine($"OnVREvent task Error: {e}", MockConsole.LogLevel.Error);
            }
            
            //Create a listener for VR events - this handles the gentle exit of SteamVR
            new Task(OnVREvent).Start();

            UIUpdater.LoadImageFromAssetFolder(true);

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
            OpenVR.Applications.RemoveApplicationManifest(_customManifest);
            OpenVR.Applications.RemoveApplicationManifest(_steamManifest);
            OpenVR.Applications.AddApplicationManifest(_customManifest, true);
            OpenVR.Applications.AddApplicationManifest(_steamManifest, true);

            // Load in the steam & custom manifest
            LoadVrManifest();
        }

        /// <summary>
        /// Wait for OpenVR to be open and connected before going any further with a wrappers' launcher sequence. No experience should
        /// be open at this point so there is no concern that killing SteamVR will exit and experience.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> WaitForOpenVR()
        {
            if (SessionController.vrHeadset == null) return false;

            MockConsole.WriteLine($"WaitForOpenVR - Checking SteamVR. Vive status: {Enum.GetName(typeof(HMDStatus), SessionController.vrHeadset.GetConnectionStatus())} " +
                $"- OpenVR status: {Manager.openVRManager?.InitialiseOpenVR() ?? false}", MockConsole.LogLevel.Normal);

            //If Vive is connect but OpenVR is not/cannot be initialised, restart SteamVR and check again.
            if (SessionController.vrHeadset.GetConnectionStatus() == HMDStatus.Connected && (!Manager.openVRManager?.InitialiseOpenVR() ?? true))
            {
                Logger.WriteLog($"OpenVRManager.WaitForOpenVR - Vive status: {SessionController.vrHeadset.GetConnectionStatus()}, " +
                    $"OpenVR connection not established - restarting SteamVR", MockConsole.LogLevel.Normal);

                //Send message to the tablet (Updating what is happening)
                SessionController.PassStationMessage($"ApplicationUpdate,Restarting SteamVR...");

                //Kill SteamVR
                CommandLine.QueryVRProcesses(new List<string> { "vrmonitor" }, true);
                await Task.Delay(5000);

                //Launch SteamVR
                SteamWrapper.LauncherSteamVR();
                await Task.Delay(3000);

                bool steamvr = await MonitorLoop(() => Process.GetProcessesByName("vrmonitor").Length == 0);
                if (!steamvr) return false;

                Logger.WriteLog($"OpenVRManager.WaitForOpenVR - Vive status: {SessionController.vrHeadset.GetConnectionStatus()}, " +
                    $"SteamVR restarted successfully", MockConsole.LogLevel.Normal);

                //Send message to the tablet (Updating what is happening)
                SessionController.PassStationMessage($"ApplicationUpdate,Connecting OpenVR...");

                bool openvr = await MonitorLoop(() => !Manager.openVRManager?.InitialiseOpenVR() ?? true);
                if (!openvr) return false;

                Logger.WriteLog($"OpenVRManager.WaitForOpenVR - Vive status: {SessionController.vrHeadset.GetConnectionStatus()}, " +
                    $"OpenVR connection established", MockConsole.LogLevel.Normal);
            }

            return true;
        }

        /// <summary>
        /// Monitors a specified condition using a loop, with optional timeout and attempt limits.
        /// </summary>
        /// <param name="conditionChecker">A delegate that returns a boolean value indicating whether the monitored condition is met.</param>
        /// <returns>True if the condition was successfully met within the specified attempts; false otherwise.</returns>
        private static async Task<bool> MonitorLoop(Func<bool> conditionChecker)
        {
            //Track the attempts
            int monitorAttempts = 0;
            int attemptLimit = 10;
            int delay = 3000;

            //Check the condition status (bail out after x amount)
            do
            {
                monitorAttempts++;
                await Task.Delay(delay);
            } while (conditionChecker.Invoke() && monitorAttempts < attemptLimit);

            // Connection bailed out, send a failure message
            if (monitorAttempts == attemptLimit)
            {
                SessionController.PassStationMessage("MessageToAndroid,HeadsetTimeout");
                return false;
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

            bool quiting = false;
            while (!quiting)
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
                        break;

                    case EVREventType.VREvent_Quit:
                        quiting = true;
                        Logger.WriteLog("SteamVR quitting", MockConsole.LogLevel.Normal);
                        _ovrSystem.AcknowledgeQuit_Exiting();
                        OpenVrSystem?.Shutdown();
                        OpenVrSystem = null;
                        UIUpdater.LoadImageFromAssetFolder(false);
                        break;
                }

                vrEvent = new VREvent_t();
            }
        }
        #endregion
        
        public void PerformDeviceChecks()
        {
            //Run through all connected devices
            CheckDevices();

            // Boundary && Controller Information (only check if the headset is connected and tracking)
            if (_tracking)
            {
                CheckBoundary();
            }
            else
            {
                //TODO this makes a call to the nuc?
                //App.UpdateDevices("Controller", "both", "Not Connected");
            }
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
                }
            }

            if (controllerCount != 0) return;
            MockConsole.WriteLine($"No controllers currently connected.", MockConsole.LogLevel.Debug);
        }

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
                    if (pchKey.Contains("steam.app") || pchKey.Contains("custom.app"))
                    {
                        // Get the application properties using the pch key
                        string applicationName =
                            GetApplicationPropertyString(pchKey, EVRApplicationProperty.Name_String);
                        string applicationLaunchType =
                            GetApplicationPropertyString(pchKey, EVRApplicationProperty.LaunchType_String);


                        string output = $"Application Key: {pchKey} " +
                                        $"Application Name: {applicationName} " +
                                        $"Application Index: {index} " +
                                        $"Application Type: {applicationLaunchType}";

                        // Check if the application is launched through Steam
                        if (pchKey.Contains("steam") && applicationLaunchType.Equals("url"))
                        {
                            // Get the Steam game ID (App ID)
                            output += " Application ID (App ID): " + pchKey.Replace("steam.app.", "");
                        }

                        Logger.WriteLog(output, MockConsole.LogLevel.Verbose);

                        vrApplicationCount++;

                        //If an application is in the dictionary it is therefore a VR experience
                        if (!_vrApplicationDictionary?.ContainsKey(applicationName) ?? false)
                        {
                            _vrApplicationDictionary?.Add(applicationName, pchKey);
                        }
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
        /// <param name="experienceName">A string of the experience to load</param>
        /// <returns>A bool if the experience is VR</returns>
        public static bool LaunchApplication(string experienceName)
        {
            if (_vrApplicationDictionary == null) return false;

            _vrApplicationDictionary.TryGetValue(experienceName.Replace("\"", ""), out var pchKey);
            if(pchKey == null) return false;
            
            EVRApplicationError error = OpenVR.Applications.LaunchApplication(pchKey);
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
            if (queriedProcessId != 0 && queriedProcessId != _processId)
            {
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
                string? currentAppStatus = Enum.GetName(typeof(EVRSceneApplicationState),
                    applications.GetSceneApplicationState());

                string output = $"Application ID: {_processId}\n" +
                                $"Application State: {currentAppStatus}\n" +
                                $"Application Type: {currentAppType}\n" +
                                $"Currently Running Application Key: {currentAppKey}\n" +
                                $"Currently Running Application Name: {currentAppName}";

                Logger.WriteLog(output, MockConsole.LogLevel.Verbose);

                // Get the process associated with the _appId
                Process targetProcess = Process.GetProcessById((int)_processId);
                
                WrapperManager.LoadWrapper(currentAppType); //Load in the appropriate wrapper type
                WrapperManager.applicationList.TryGetValue(currentAppKey.Split(".")[2], out var experience);
                WrapperManager.CurrentWrapper?.SetLastExperience(experience);
                WrapperManager.CurrentWrapper?.SetCurrentProcess(targetProcess); //Sets the wrapper process and calls WaitForExit
                WrapperManager.CurrentWrapper?.SetLaunchingExperience(false);
                
                WindowManager.MaximizeProcess(targetProcess); //Maximise the process experience

                string? experienceId = experience.ID;
                if (string.IsNullOrEmpty(experienceId))
                {
                    experienceId = "0";
                }
                
                // Send a message to the NUC
                SessionController.PassStationMessage(
                    $"ApplicationUpdate,{currentAppName}/{experienceId}/{currentAppType}");

                // Update the Station UI
                UIUpdater.UpdateProcess(targetProcess.MainWindowTitle);
                UIUpdater.UpdateStatus("Running...");
            }
        }
        #endregion

        #region Headset Information
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
            MockConsole.WriteLine($"CVRChaperone CalibrationState: {chaperone.GetCalibrationState()}", MockConsole.LogLevel.Verbose);
        }

        /// <summary>
        /// Obtains the position and orientation of the VR headset (HMD) using OpenVR SDK.
        /// Updates the "App" object with the tracking status of the headset.
        /// Outputs headset tracking information to the console if debug mode is enabled.
        /// </summary>
        /// <param name="headsetIndex">The index of the tracked device to query.</param>
        private void GetHeadsetPositionAndOrientation(uint headsetIndex)
        {
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
            string output = $"bDeviceIsConnected: {poses[headsetIndex].bDeviceIsConnected}\n" +
                            $"bPoseIsValid: {poses[headsetIndex].bPoseIsValid}\n" +
                            $"trackingStatus: {poses[headsetIndex].eTrackingResult}\n" +
                            $"Headset Position: {headsetPosition}\n" +
                            $"Headset Orientation: {headsetOrientation}";

            Logger.WriteLog(output, MockConsole.LogLevel.Verbose);

            if (headsetPosition == new Vector3(0, 0, 0) && headsetOrientation == new Quaternion(1, 0, 0, 0) &&
                _tracking)
            {
                _tracking = false;
                //TODO send this information to the nuc?
                SessionController.vrHeadset?.SetOpenVRStatus(HMDStatus.Lost);
                MockConsole.WriteLine("Headset lost", MockConsole.LogLevel.Normal);
            }
            else if (headsetPosition != new Vector3(0, 0, 0) && headsetOrientation != new Quaternion(1, 0, 0, 0) &&
                     !_tracking)
            {
                _tracking = true;
                //TODO send this information to the nuc?
                SessionController.vrHeadset?.SetOpenVRStatus(HMDStatus.Connected);
                MockConsole.WriteLine("Headset found", MockConsole.LogLevel.Normal);
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
            if (_ovrSystem == null)
            {
                return;
            }

            // Create a StringBuilder to hold the controller description
            StringBuilder descriptionBuilder = new StringBuilder(256); // You can adjust the buffer size as needed

            // Get the controller role (left or right)
            ETrackedControllerRole role = _ovrSystem.GetControllerRoleForTrackedDeviceIndex(controllerIndex);

            // Get the controller description
            ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
            _ovrSystem.GetStringTrackedDeviceProperty(controllerIndex,
                ETrackedDeviceProperty.Prop_RenderModelName_String, descriptionBuilder,
                (uint)descriptionBuilder.Capacity, ref error);
            string description = descriptionBuilder.ToString();

            // Get the controller battery percentage as a float value
            float batteryLevel = _ovrSystem.GetFloatTrackedDeviceProperty(controllerIndex,
                ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);

            // Check if the battery percentage is valid (error is TrackedProp_Success)
            if (error == ETrackedPropertyError.TrackedProp_Success)
            {
                // Do something with the controller information
                string controllerRole = role == ETrackedControllerRole.LeftHand ? "Left" : "Right";
                int formattedBatteryLevel = (int)(batteryLevel * 100);
                //TODO send this information to the nuc?
                MockConsole.WriteLine(
                    $"Controller {controllerIndex} (Role: {controllerRole}) - Description: {description}, Battery Level: {formattedBatteryLevel}%", 
                    MockConsole.LogLevel.Verbose);

                //App.UpdateDevices("Controller", controllerRole, $"Battery Level: {formattedBatteryLevel}%");
            }
            else
            {
                // Handle the case when battery level retrieval fails
                //TODO send this information to the nuc?
                MockConsole.WriteLine(
                    $"Failed to get the battery level for controller {controllerIndex}. Error: {error}", 
                    MockConsole.LogLevel.Verbose);
            }
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
        #endregion
    }
}
