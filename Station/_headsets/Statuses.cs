﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Station
{
    /// <summary>
    /// A class designed to hold the statuses of the different connected VR devices. This class belongs to a headset
    /// as the headset is required for connection to SteamVR before other statuses can be determined. The statuses
    /// included are:
    /// Software Management Status - the software required to manage the headset outside of SteamVR.
    /// OpenVR Status - OpenVR's current status of the headset.
    /// Controller Statuses - The roll of (left/right) and statues of connected controllers.
    /// Base Stations - The number of and current status of the connected base stations.
    /// </summary>
    public class Statuses
    {
        public string HeadsetDescription { private set; get; } = "Unknown";

        //Controller models stored by serial number
        public static Dictionary<string, VrController> controllers = new();
        //Base Station models stored by serial number
        public static Dictionary<string, VrBaseStation> baseStations = new();

        #region Observers
        /// <summary>
        /// External software that is required to link the headset to SteamVR
        ///     Vive Pro 1 - Determined by Vive Logs
        ///     Vive Pro 2 - Determined by Vive Console
        /// </summary>
        private DeviceStatus _softwareStatus = DeviceStatus.Off;
        public DeviceStatus SoftwareStatus
        {
            private set
            {
                if (_softwareStatus == value) return;

                OnSoftwareTrackingChanged(value.ToString());
                _softwareStatus = value;
            }
            get
            {
                return _softwareStatus;
            }
        }

        public event EventHandler<GenericEventArgs<string>>? SoftwareTrackingChanged;
        protected virtual void OnSoftwareTrackingChanged(string newValue)
        {
            MockConsole.WriteLine($"DeviceStatus:Headset:tracking:Vive:{newValue}", MockConsole.LogLevel.Debug);
            string message = $"Headset:Vive:tracking:{newValue}";
            SoftwareTrackingChanged?.Invoke(this, new GenericEventArgs<string>(message));
        }

        /// <summary>
        /// OpenVR's wrapper around SteamVR for tracking the headset.
        /// </summary>
        private DeviceStatus _openVRStatus = DeviceStatus.Off;
        public DeviceStatus OpenVRStatus
        {
            private set
            {
                if (_openVRStatus == value) return;

                OnOpenVRTrackingChanged(value.ToString());
                _openVRStatus = value;
                UIUpdater.UpdateOpenVRStatus("headsetConnection", Enum.GetName(typeof(DeviceStatus), value));
            }
            get
            {
                return _openVRStatus;
            }
        }

        public event EventHandler<GenericEventArgs<string>>? OpenVRTrackingChanged;
        protected virtual void OnOpenVRTrackingChanged(string newValue)
        {
            MockConsole.WriteLine($"DeviceStatus:Headset:tracking:OpenVR:{newValue}", MockConsole.LogLevel.Debug);
            string message = $"Headset:OpenVR:tracking:{newValue}";
            OpenVRTrackingChanged?.Invoke(this, new GenericEventArgs<string>(message));
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
        /// <typeparam name="T">The type of the value that changed.</typeparam>
        /// <param name="sender">The object that triggered the event.</param>
        /// <param name="e">Event arguments containing information about the value change.</param>
        public void HandleValueChanged(object? sender, GenericEventArgs<string> e)
        {
            // Code to execute when the value changes.
            Manager.SendResponse("Android", "Station", $"DeviceStatus:{e.Data}");
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
                MockConsole.LogLevel.Debug);

            if (manager == VrManager.Software)
            {
                SoftwareStatus = status;
            }
            else if (manager == VrManager.OpenVR)
            {
                OpenVRStatus = status;
            }
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
            if (controllers.ContainsKey(serialNumber))
            {
                if (controllers.TryGetValue(serialNumber, out var temp) && temp != null)
                {
                    temp.UpdateProperty(propertyName, value);
                }
                else
                {
                    Logger.WriteLog($"VrStatus.UpdateController - A Controller entry is invalid removing {serialNumber}",
                        MockConsole.LogLevel.Error);

                    controllers.Remove(serialNumber);
                }
            }
            else if (role != null)
            {
                bool duplicate = controllers.Any(vrController => vrController.Value.Role == role);

                //Invalidate the controllers dictionary if there are multiple of the same role. (i.e two lefts or two rights)
                if (duplicate)
                {
                    foreach (var vrController in controllers)
                    {
                        if (vrController.Value.Role == role)
                        {
                            vrController.Value.UpdateProperty("battery", 0);
                            vrController.Value.UpdateProperty("tracking", DeviceStatus.Lost);
                            MockConsole.WriteLine($"Duplicate controller - Role: {Enum.GetName(typeof(DeviceRole), role)}. Reseting dictionary.", MockConsole.LogLevel.Normal);
                        }
                    }
                    controllers.Clear();
                    return;
                }

                //Add a new controller entry
                MockConsole.WriteLine($"Found a new controller: {serialNumber} - Role: {Enum.GetName(typeof(DeviceRole), role)}", MockConsole.LogLevel.Normal);
                VrController temp = new VrController(serialNumber, (DeviceRole)role);
                temp.UpdateProperty(propertyName, value);
                controllers.Add(serialNumber, temp);
            }
            else
            {
                //If there is no entry and no role then do not add the controller yet
                MockConsole.WriteLine($"Found a new controller: {serialNumber} - Role invalid: {role}, not adding.", MockConsole.LogLevel.Normal);
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
                        MockConsole.LogLevel.Error);

                    baseStations.Remove(serialNumber);
                }
            }
            else
            {
                //Add a new base station entry
                MockConsole.WriteLine($"Found a new base station: {serialNumber}", MockConsole.LogLevel.Normal);
                VrBaseStation temp = new VrBaseStation(serialNumber);
                temp.UpdateProperty(propertyName, value);
                baseStations.Add(serialNumber, temp);
                UIUpdater.UpdateOpenVRStatus("baseStationAmount", baseStations.Count.ToString());
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
            UIUpdater.UpdateOpenVRStatus("headsetDescription", HeadsetDescription);

            //Reset controllers
            foreach (var vrController in controllers)
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
        public void QueryStatues()
        {
            //Headset
            Manager.SendResponse("Android", "Station", $"DeviceStatus:Headset:OpenVR:tracking:{OpenVRStatus.ToString()}");
            Manager.SendResponse("Android", "Station", $"DeviceStatus:Headset:Vive:tracking:{SoftwareStatus.ToString()}");

            //Controllers
            foreach (var vrController in controllers)
            {
                //Update the tablet
                Manager.SendResponse("Android", "Station", $"DeviceStatus:Controller:{vrController.Value.Role.ToString()}:tracking:{vrController.Value.Tracking.ToString()}");
                Manager.SendResponse("Android", "Station", $"DeviceStatus:Controller:{vrController.Value.Role.ToString()}:battery:{vrController.Value.Battery}");
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
            Manager.SendResponse("Android", "Station", $"DeviceStatus:BaseStation:{active}:{baseStations.Count}");
        }
    }
}
