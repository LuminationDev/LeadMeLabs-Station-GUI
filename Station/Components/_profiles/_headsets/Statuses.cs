using System;
using System.Collections.Generic;
using System.Linq;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._commandLine;
using Station.Components._enums;
using Station.Components._interfaces;
using Station.Components._legacy;
using Station.Components._managers;
using Station.Components._models;
using Station.Components._notification;
using Station.Components._utils;
using Station.Components._version;
using Station.MVC.Controller;
using Station.QA;

namespace Station.Components._profiles._headsets;

/// <summary>
/// A class designed to hold the statuses of the different connected VR devices. This class belongs to a headset
/// as the headset is required for connection to SteamVR before other statuses can be determined. The statuses
/// included are:
/// Software Management Status - the software required to manage the headset outside of SteamVR.
/// OpenVR Status - OpenVRs current status of the headset.
/// Controller Statuses - The roll of (left/right) and statuses of connected controllers.
/// Base Stations - The number of and current status of the connected base stations.
/// </summary>
public class Statuses
{
    public string HeadsetDescription { private set; get; } = "Unknown";

    //Controller models stored by serial number
    private static readonly Dictionary<string, VrController> Controllers = new();
    //Base Station models stored by serial number
    public static Dictionary<string, VrBaseStation> baseStations = new();
    public static Dictionary<string, VrTracker> trackers = new();

    private static Boolean openVrHasAlreadyConnected = false;

    #region Observers
    /// <summary>
    /// External software that is required to link the headset to SteamVR
    ///     Vive Pro 1      - Determined by Vive Logs
    ///     Vive Pro 2      - Determined by Vive Console
    ///     Vive Focus 3    - Determined by Vive Business Streaming
    /// </summary>
    private DeviceStatus _softwareStatus = DeviceStatus.Off;
    public DeviceStatus SoftwareStatus
    {
        private set
        {
            if (_softwareStatus == value) return;

            OnSoftwareTrackingChanged(value.ToString());
            _softwareStatus = value;
            UiController.UpdateVrConnection("thirdParty", Enum.GetName(typeof(DeviceStatus), value) ?? "Unknown");
            //Silently collect the applications again if the Steam manifest was corrupted
            if (value == DeviceStatus.Connected && WrapperManager.steamManifestCorrupted)
            {
                WrapperManager.SilentlyCollectApplications();
            }
        }
        get => _softwareStatus;
    }

    public event EventHandler<GenericEventArgs<string>>? SoftwareTrackingChanged;
    protected virtual void OnSoftwareTrackingChanged(string newValue)
    {
        MockConsole.WriteLine($"DeviceStatus:Headset:tracking:Vive:{newValue}", Enums.LogLevel.Debug);
        string message = $"Headset:Vive:tracking:{newValue}";
        SoftwareTrackingChanged?.Invoke(this, new GenericEventArgs<string>(message));
        
        AudioManager.Initialise();
    }

    /// <summary>
    /// OpenVR's wrapper around SteamVR for tracking the headset.
    /// </summary>
    private DeviceStatus _openVRStatus = DeviceStatus.Off;
    public DeviceStatus OpenVRStatus
    {
        private set
        {
            MockConsole.WriteLine($"New connection: {value}", Enums.LogLevel.Verbose);
            if (_openVRStatus == value) return;

            OnOpenVRTrackingChanged(value.ToString());
            _openVRStatus = value;
            UiController.UpdateVrConnection("openVr", Enum.GetName(typeof(DeviceStatus), value) ?? "Unknown");
        }
        get => _openVRStatus;
    }

    public event EventHandler<GenericEventArgs<string>>? OpenVRTrackingChanged;
    protected virtual void OnOpenVRTrackingChanged(string newValue)
    {
        MockConsole.WriteLine($"DeviceStatus:Headset:tracking:OpenVR:{newValue}", Enums.LogLevel.Debug);
        string message = $"Headset:OpenVR:tracking:{newValue}";
        OpenVRTrackingChanged?.Invoke(this, new GenericEventArgs<string>(message));
        
        // close the legacy mirror on the first time we launch SteamVR (in case it stayed open from a previous crash)
        if (!openVrHasAlreadyConnected)
        {
            openVrHasAlreadyConnected = true;
            // close legacy mirror if open
            if (StationCommandLine.GetProcessIdFromMainWindowTitle("Legacy Mirror") != null)
            {
                StationCommandLine.ToggleSteamVrLegacyMirror();
            }
        }
    }
    #endregion

    public Statuses()
    {
        SoftwareTrackingChanged += HandleValueChanged;
        OpenVRTrackingChanged += HandleValueChanged;
    }

    /// <summary>
    /// Set the model name of the headset.
    /// </summary>
    /// <param name="description"></param>
    public void SetHeadsetDescription(string description)
    {
        HeadsetDescription = description;
    }

    /// <summary>
    /// Event handler method called when the value changes.
    /// </summary>
    /// <param name="sender">The object that triggered the event.</param>
    /// <param name="e">Event arguments containing information about the value change.</param>
    public void HandleValueChanged(object? sender, GenericEventArgs<string> e)
    {
        // Code to execute when the value changes.
        // LegacySetValue required here as deviceStatus is handled differently going forward
        if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
        {
            LegacySetValue.SimpleSetValue("deviceStatus", e.Data);
        }
        else
        {
            string[] keyValue = e.Data.Split(":", 4);
            switch (keyValue[0])
            {
                case "Headset":
                    if (keyValue[2].Equals("tracking"))
                    {
                        StateController.UpdateStateValue(
                            keyValue[1].Equals("OpenVR") ? "openVRHeadsetTracking" : "thirdPartyHeadsetTracking",
                            keyValue[3]);
                    }
                    break;
            
                case "Controller":
                    switch (keyValue[2])
                    {
                        case "tracking":
                            switch (keyValue[1])
                            {
                                case "Left":
                                    StateController.UpdateStateValue("leftControllerTracking", keyValue[3]);
                                    break;
                                case "Right":
                                    StateController.UpdateStateValue("rightControllerTracking", keyValue[3]);
                                    break;
                            }

                            break;
                        case "battery":
                        {
                            switch (keyValue[1])
                            {
                                case "Left":
                                    StateController.UpdateStateValue("leftControllerBattery", keyValue[3]);
                                    break;
                                case "Right":
                                    StateController.UpdateStateValue("rightControllerBattery", keyValue[3]);
                                    break;
                            }

                            break;
                        }
                    }
                    break;
            
                case "BaseStation":
                    Dictionary<string, object> baseStationValues = new()
                    {
                        { "baseStationsActive", int.Parse(keyValue[1]) },
                        { "baseStationsTotal", int.Parse(keyValue[2]) }
                    };
                    
                    StateController.UpdateStatusBunch(baseStationValues);
                    break;
                
                case "Tracker":
                    Dictionary<string, object> trackerValues = new()
                    {
                        { "trackersActive", int.Parse(keyValue[1]) },
                        { "trackersTotal", int.Parse(keyValue[2]) }
                    };
                    
                    StateController.UpdateStatusBunch(trackerValues);
                    break;
            }
        }
    }

    private bool _headsetFirmwareStatus = false;
    public void UpdateHeadsetFirmwareStatus(bool status)
    {
        this._headsetFirmwareStatus = status;
    }

    /// <summary>
    /// Update the VR headset status, there are two managers, one specific to the headset and
    /// OpenVR.
    /// </summary>
    /// <param name="manager">A VrManager enum of what application connection status is being updated.</param>
    /// <param name="status">A HMDStatus enum of the connection status (e.g. Connected, Lost</param>
    public void UpdateHeadset(VrManager manager, DeviceStatus status)
    {
        MockConsole.WriteLine($"Updating Headset:{Enum.GetName(typeof(VrManager), manager)}:{Enum.GetName(typeof(DeviceStatus), status)}", 
            Enums.LogLevel.Debug);

        switch (manager)
        {
            case VrManager.Software:
                SendLostMessage(SoftwareStatus == DeviceStatus.Connected, status);
                SoftwareStatus = status;
                UiController.UpdateVrUi("headset", Enum.GetName(typeof(DeviceStatus), status) ?? "Off");
                break;
            
            case VrManager.OpenVR:
                SendLostMessage(OpenVRStatus == DeviceStatus.Connected, status);
                OpenVRStatus = status;
                break;
        }
        
        SendReadyMessage();
    }

    /// <summary>
    /// Send a message to the tablet stating the headset is ready if both the SoftwareStatus and the OpenVRStatus
    /// are 'Connected'
    /// </summary>
    private void SendReadyMessage()
    {
        //If the headset is connected and no experience is currently running tell the tablet the Station is ready to go
        if (SoftwareStatus == DeviceStatus.Connected && OpenVRStatus == DeviceStatus.Connected && SessionController.CurrentState != State.Ready)
        {
            ScheduledTaskQueue.EnqueueTask(() => SessionController.UpdateState(State.Ready), TimeSpan.FromSeconds(0));
        }
    }
    
    /// <summary>
    /// Send a message to the tablet stating the headset has been lost if the SoftwareStatus or the OpenVRStatus
    /// are 'Lost' or 'Off'.
    /// </summary>
    private void SendLostMessage(bool oldConnectionStatus, DeviceStatus newConnectionStatus)
    {
        if (!oldConnectionStatus || newConnectionStatus is not (DeviceStatus.Lost or DeviceStatus.Off)) return;
        
        State state = WrapperManager.currentWrapper?.GetCurrentExperienceName() == null ? State.Awaiting : State.Lost;
        ScheduledTaskQueue.EnqueueTask(() => SessionController.UpdateState(state), TimeSpan.FromSeconds(0));
    }

    /// <summary>
    /// Update a VR controller.
    /// </summary>
    /// <param name="serialNumber"></param>
    /// <param name="role"></param>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    public void UpdateController(string serialNumber, DeviceRole? role, string propertyName, object value)
    {
        //Check if the entry exists
        if (Controllers.ContainsKey(serialNumber))
        {
            if (Controllers.TryGetValue(serialNumber, out var temp) && temp != null)
            {
                temp.UpdateProperty(propertyName, value);
            }
            else
            {
                Logger.WriteLog($"VrStatus.UpdateController - A Controller entry is invalid removing {serialNumber}",
                    Enums.LogLevel.Error);

                Controllers.Remove(serialNumber);
            }
        }
        else if (role != null)
        {
            bool duplicate = Controllers.Any(vrController => vrController.Value.Role == role);

            //Invalidate the controllers dictionary if there are multiple of the same role. (i.e two lefts or two rights)
            if (duplicate)
            {
                foreach (var vrController in Controllers)
                {
                    if (vrController.Value.Role == role)
                    {
                        vrController.Value.UpdateProperty("battery", 0);
                        vrController.Value.UpdateProperty("tracking", DeviceStatus.Lost);
                        MockConsole.WriteLine($"Duplicate controller - Role: {Enum.GetName(typeof(DeviceRole), role)}. Reseting dictionary.", Enums.LogLevel.Normal);
                    }
                }
                Controllers.Clear();
                return;
            }

            //Add a new controller entry
            MockConsole.WriteLine($"Found a new controller: {serialNumber} - Role: {Enum.GetName(typeof(DeviceRole), role)}", Enums.LogLevel.Normal);
            VrController temp = new VrController(serialNumber, (DeviceRole)role);
            temp.UpdateProperty(propertyName, value);
            Controllers.Add(serialNumber, temp);
        }
        else
        {
            //If there is no entry and no role then do not add the controller yet
            MockConsole.WriteLine($"Found a new controller: {serialNumber} - Role invalid: {role}, not adding.", Enums.LogLevel.Normal);
        }
    }

    /// <summary>
    /// Update a VR base station.
    /// </summary>
    /// <param name="serialNumber"></param>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    public void UpdateBaseStation(string serialNumber, string propertyName, object value)
    {
        //Check if the entry exists
        if (baseStations.ContainsKey(serialNumber))
        {
            if (baseStations.TryGetValue(serialNumber, out var temp) && temp != null)
            {
                temp.UpdateProperty(propertyName, value);
            }
            else
            {
                Logger.WriteLog($"VrStatus.UpdateBaseStation - A Base Station entry is invalid removing {serialNumber}",
                    Enums.LogLevel.Error);

                baseStations.Remove(serialNumber);
            }
        }
        else
        {
            //Add a new base station entry
            MockConsole.WriteLine($"Found a new base station: {serialNumber}", Enums.LogLevel.Normal);
            VrBaseStation temp = new VrBaseStation(serialNumber);
            temp.UpdateProperty(propertyName, value);
            baseStations.Add(serialNumber, temp);
        }
        
        //TODO update for Vive Business Streaming headsets
        int count = baseStations.Values.Count(vr => vr.Tracking == DeviceStatus.Connected);
        string num = count switch
        {
            0 => "Off",
            < 3 => "Lost",
            _ => "Connected"
        };
        UiController.UpdateVrUi("baseStation", num);
    }
    
    /// <summary>
    /// Update a tracker.
    /// </summary>
    /// <param name="serialNumber"></param>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    public void UpdateTracker(string serialNumber, string propertyName, object value)
    {
        //Check if the entry exists
        if (trackers.ContainsKey(serialNumber))
        {
            if (trackers.TryGetValue(serialNumber, out var temp) && temp != null)
            {
                temp.UpdateProperty(propertyName, value);
            }
            else
            {
                Logger.WriteLog($"VrStatus.Tracker - A tracker entry is invalid removing {serialNumber}",
                    Enums.LogLevel.Error);

                trackers.Remove(serialNumber);
            }
        }
        else
        {
            //Add a new base station entry
            MockConsole.WriteLine($"Found a new tracker: {serialNumber}", Enums.LogLevel.Normal);
            VrTracker temp = new VrTracker(serialNumber);
            trackers.Add(serialNumber, temp);
            
            //TODO add this to the UI
            //UiUpdater.UpdateOpenVrStatus("trackerAmount", trackers.Count.ToString());
            if (trackers.TryGetValue(serialNumber, out var temp1) && temp != null)
            {
                temp1.UpdateProperty(propertyName, value);
            }
        }
    }

    /// <summary>
    /// Reset all the statuses in the event of a headset drop or when the VR system is restarted.
    /// </summary>
    public void ResetStatuses()
    {
        //Reset headset
        HeadsetDescription = "Unknown";
        SoftwareStatus = DeviceStatus.Off;
        OpenVRStatus = DeviceStatus.Off;
        UiController.UpdateHeadsetDescription(HeadsetDescription);

        //Reset controllers
        foreach (var vrController in Controllers)
        {
            vrController.Value.UpdateProperty("battery", 0);
            vrController.Value.UpdateProperty("tracking", DeviceStatus.Off);
        }

        //Reset base stations
        foreach (var vrBaseStation in baseStations)
        {
            vrBaseStation.Value.UpdateProperty("tracking", DeviceStatus.Off);
        }
    }

    /// <summary>
    /// Re-send the current statuses of all VR devices. This is used when a tablet is connecting/reconnecting to the NUC.
    /// </summary>
    public void QueryStatuses()
    {
        // LegacySetValue required here as deviceStatus is handled differently going forward
        // Code to execute when the value changes.
        if (VersionHandler.NucVersion < LeadMeVersion.StateHandler)
        {
            //Headset
            LegacySetValue.SimpleSetValue("deviceStatus", $"Headset:OpenVR:tracking:{OpenVRStatus.ToString()}");
            LegacySetValue.SimpleSetValue("deviceStatus", $"Headset:Vive:tracking:{SoftwareStatus.ToString()}");

            //Controllers
            foreach (var vrController in Controllers)
            {
                //Update the tablet
                LegacySetValue.SimpleSetValue("deviceStatus", $"Controller:{vrController.Value.Role.ToString()}:tracking:{vrController.Value.Tracking.ToString()}");
                LegacySetValue.SimpleSetValue("deviceStatus", $"Controller:{vrController.Value.Role.ToString()}:battery:{vrController.Value.Battery}");
            }

            //Base stations
            int active = 0;
            foreach (var vrBaseStation in baseStations)
            {
                if (vrBaseStation.Value.Tracking == DeviceStatus.Connected)
                {
                    active++;
                }
            }
            LegacySetValue.SimpleSetValue("deviceStatus", $"BaseStation:{active}:{baseStations.Count}");
        }
        else
        {
            Dictionary<string, object?> stateValues = new()
            {
                {"openVRHeadsetTracking", OpenVRStatus.ToString()},
                {"thirdPartyHeadsetTracking", SoftwareStatus.ToString()},
            };

            foreach (var vrController in Controllers)
            {
                switch (vrController.Value.Role.ToString())
                {
                    case "left":
                        stateValues.Add("leftControllerBattery", vrController.Value.Battery);
                        stateValues.Add("leftControllerTracking", vrController.Value.Tracking.ToString());
                        break;
                    case "right":
                        stateValues.Add("rightControllerBattery", vrController.Value.Battery);
                        stateValues.Add("rightControllerTracking", vrController.Value.Tracking.ToString());
                        break;
                }
            }
            
            int active = 0;
            foreach (var vrBaseStation in baseStations)
            {
                if (vrBaseStation.Value.Tracking == DeviceStatus.Connected)
                {
                    active++;
                }
            }
            stateValues.Add("baseStationsActive", active);
            stateValues.Add("baseStationsTotal", baseStations.Count);

            StateController.UpdateStatusBunch(stateValues);
        }
    }

    public JObject GetStatusesJson()
    {
        JObject vrStatuses = new JObject
        {
            { "openVrStatus", OpenVRStatus.ToString() },
            { "headsetStatus", SoftwareStatus.ToString() }
        };

        // each controller
        JArray controllersJArray = new JArray();
        foreach (var vrController in Controllers)
        {
            JObject controller = new JObject
            {
                { "tracking", vrController.Value.Tracking.ToString() },
                { "battery", vrController.Value.Battery }
            };
            controllersJArray.Add(controller);
        }
        vrStatuses.Add("controllers", controllersJArray);
        
        int active = baseStations.Count(vrBaseStation => vrBaseStation.Value.Tracking == DeviceStatus.Connected);
        vrStatuses.Add("connectedBaseStations", active);

        return vrStatuses;
    }

    public List<QaCheck> VrQaChecks()
    {
        List<QaCheck> qaChecks = new List<QaCheck>();

        QaCheck headsetConnected = new QaCheck("headset_connected");
        QaCheck headsetFirmwareUpToDate = new QaCheck("headset_firmware");
        QaCheck controllersConnected = new QaCheck("controllers_connected");
        QaCheck controllersFirmware = new QaCheck("controllers_firmware");
        QaCheck baseStationsConnected = new QaCheck("base_stations_connected");
        QaCheck baseStationsFirmware = new QaCheck("base_stations_firmware");

        if (!Helper.GetStationMode().Equals(Helper.STATION_MODE_VR))
        {
            headsetConnected.SetPassed("Station is a non-vr station");
            headsetFirmwareUpToDate.SetPassed("Station is a non-vr station");
            controllersConnected.SetPassed("Station is a non-vr station");
            controllersFirmware.SetPassed("Station is a non-vr station");
            baseStationsConnected.SetPassed("Station is a non-vr station");
            baseStationsFirmware.SetPassed("Station is a non-vr station");
            
            qaChecks.Add(headsetConnected);
            qaChecks.Add(headsetFirmwareUpToDate);
            qaChecks.Add(controllersConnected);
            qaChecks.Add(controllersFirmware);
            qaChecks.Add(baseStationsConnected);
            qaChecks.Add(baseStationsFirmware);
            return qaChecks;
        }
        
        if (OpenVRStatus == DeviceStatus.Connected && SoftwareStatus == DeviceStatus.Connected)
        {
            headsetConnected.SetPassed(null);
        }
        else
        {
            headsetConnected.SetFailed("Headset not connected");
        }
        
        if (OpenVRStatus != DeviceStatus.Connected || SoftwareStatus != DeviceStatus.Connected)
        {
            headsetFirmwareUpToDate.SetFailed("Headset not connected");
        }
        else
        {
            if (_headsetFirmwareStatus)
            {
                headsetFirmwareUpToDate.SetFailed("Headset has a firmware update");
            }
            else
            {
                headsetFirmwareUpToDate.SetPassed(null);
            }
        }
        
        if (Controllers.Count(controller => controller.Value.Tracking == DeviceStatus.Connected) >= 2)
        {
            controllersConnected.SetPassed(null);
        }
        else
        {
            controllersConnected.SetFailed("Could not find two controllers that are tracking");
        }
        
        if (OpenVRStatus != DeviceStatus.Connected || SoftwareStatus != DeviceStatus.Connected)
        {
            controllersFirmware.SetFailed("Headset not connected");
        } else if (Controllers.Count(controller => controller.Value.Tracking == DeviceStatus.Connected) < 2)
        {
            controllersFirmware.SetFailed("Less than two controllers connected");
        }
        else
        {
            if (Controllers.Count(controller => controller.Value.FirmwareUpdateRequired()) < 2)
            {
                controllersFirmware.SetPassed(null);
            }
            else
            {
                controllersFirmware.SetFailed("At least one controller needs a firmware update");
            }
        }
        
        if (baseStations.Count(baseStation => baseStation.Value.Tracking == DeviceStatus.Connected) >= 2)
        {
            baseStationsConnected.SetPassed(null);
        }
        else
        {
            baseStationsConnected.SetFailed("Could not find two base stations that are tracking");
        }
        
        
        if (OpenVRStatus != DeviceStatus.Connected || SoftwareStatus != DeviceStatus.Connected)
        {
            baseStationsFirmware.SetFailed("Headset not connected");
        } else if (baseStations.Count(baseStation => baseStation.Value.Tracking == DeviceStatus.Connected) < 2)
        {
            baseStationsFirmware.SetFailed("Less than two base stations connected");
        }
        else
        {
            if (baseStations.Count(baseStation => baseStation.Value.FirmwareUpdateRequired()) < 2)
            {
                baseStationsFirmware.SetPassed(null);
            }
            else
            {
                baseStationsFirmware.SetFailed("At least one base station needs a firmware update");
            }
        }

        qaChecks.Add(headsetConnected);
        qaChecks.Add(headsetFirmwareUpToDate);
        qaChecks.Add(controllersConnected);
        qaChecks.Add(controllersFirmware);
        qaChecks.Add(baseStationsConnected);
        qaChecks.Add(baseStationsFirmware);
        return qaChecks;
    }
}
