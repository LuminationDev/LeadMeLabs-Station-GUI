using System;
using System.Collections.Generic;

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
        public string HeadsetDescription { private set; get; } = "";

        //Controller models stored by serial number
        private Dictionary<string, VrController> controllers = new();
        //Base Station models stored by serial number
        private Dictionary<string, VrBaseStation> baseStations = new();

        //If the status for a device has not been set through after (pollLimit) is reached
        //resend the statuses for that device to make sure everything is up to date.
        //Controllers are tracked within their class as they have multiple properties updating
        //with 2 different controllers being connected.
        public static int pollLimit = 15;
        private int headsetCount = 0;
        private int baseStationCount = 0;

        /// <summary>
        /// External software that is required to link the headset to SteamVR
        ///     Vive Pro 1 - Determined by Vive Logs
        ///     Vive Pro 2 - Determined by Vive Console
        /// </summary>
        public DeviceStatus SoftwareStatus { private set; get; } = DeviceStatus.Lost;

        /// <summary>
        /// OpenVR's wrapper around SteamVR. Overrides SoftwareStatus if the WrapperMonitoringThread is not 
        /// initialised.
        /// </summary>
        public DeviceStatus OpenVRStatus { private set; get; } = DeviceStatus.Lost;

        /// <summary>
        /// Set the model name of the headset.
        /// </summary>
        /// <param name="description"></param>
        public void SetHeadsetDescription(string description)
        {
            HeadsetDescription = description;
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
                MockConsole.LogLevel.Normal);

            //Determine if the model should update and have the value sent to the NUC
            bool shouldUpdate = false;

            if (manager == VrManager.Software)
            {
                shouldUpdate = SoftwareStatus != status;
                SoftwareStatus = status;
            }
            else if (manager == VrManager.OpenVR)
            {
                MockConsole.WriteLine($"OpenVR {OpenVRStatus} - Status {status}", MockConsole.LogLevel.Normal);
                
                shouldUpdate = OpenVRStatus != status;
                OpenVRStatus = status;
            }

            //If just one of the headset statuses is set to lost and the Wrapper is actively monitoring the
            //third party software status then the headset is considered lost.
            if (WrapperMonitoringThread.monitoring)
            {
                if (SoftwareStatus == DeviceStatus.Lost || OpenVRStatus == DeviceStatus.Lost)
                {
                    status = DeviceStatus.Lost;
                }
            }
            else if (OpenVRStatus == DeviceStatus.Lost)
            {
                status = DeviceStatus.Lost;
            }

            //Send a message to the NUC if necessary
            if (shouldUpdate || headsetCount > pollLimit)
            {
                headsetCount = 0;
                string connection = Enum.GetName(typeof(DeviceStatus), status);
                UIUpdater.UpdateOpenVRStatus("headsetConnection", connection);

                MockConsole.WriteLine($"DeviceStatus:Headset:tracking:{connection}", MockConsole.LogLevel.Debug);
                Manager.SendResponse("Android", "Station", $"DeviceStatus:Headset:tracking:{connection}");
            } else
            {
                headsetCount++;
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
            //Determine if the model should update and have the value sent to the NUC
            bool shouldUpdate;

            //Check if the entry exists
            if (controllers.ContainsKey(serialNumber))
            {
                controllers.TryGetValue(serialNumber, out var temp);
                if (temp == null)
                {
                    //Error has occurred
                    Logger.WriteLog($"VrStatus.UpdateController - A Controller entry is invalid removing {serialNumber}",
                        MockConsole.LogLevel.Error);

                    //Clear the invalid entry
                    controllers.Remove(serialNumber);
                    return;
                }

                shouldUpdate = temp.UpdateProperty(propertyName, value);
            }
            else if (role != null)
            {
                bool duplicate = false;

                //Invalidate the controllers dictionary if there are multiple of the same role. (i.e two lefts or two rights)
                foreach (var vrController in controllers)
                {
                    if(vrController.Value.Role == role)
                    {
                        duplicate = true;
                        vrController.Value.UpdateProperty("tracking", DeviceStatus.Lost);
                        vrController.Value.UpdateProperty("battery", 0);
                        Manager.SendResponse("Android", "Station", $"DeviceStatus:Controller:{vrController.Value.Role.ToString()}:tracking:{DeviceStatus.Lost}");
                        MockConsole.WriteLine($"Duplicate controller - Role: {Enum.GetName(typeof(DeviceRole), role)}. Reseting dictionary.", MockConsole.LogLevel.Normal);
                        controllers = new();
                    }
                }
                if (duplicate) return;

                //Add a new controller entry
                MockConsole.WriteLine($"Found a new controller: {serialNumber} - Role: {Enum.GetName(typeof(DeviceRole), role)}", MockConsole.LogLevel.Normal);
                VrController temp = new VrController(serialNumber, role);
                shouldUpdate = temp.UpdateProperty(propertyName, value);
                controllers.Add(serialNumber, temp);
            }
            else
            {
                //If there is no entry and no role then do not add the controller yet
                MockConsole.WriteLine($"Found a new controller: {serialNumber} - Role invalid: {role}, not adding.", MockConsole.LogLevel.Normal);
                return;
            }
            
            //Send a message to the NUC if necessary
            if (shouldUpdate) //limit * property values update * number of controllers
            {
                controllers.TryGetValue(serialNumber, out var current);
                if (current == null) return;
                
                MockConsole.WriteLine($"DeviceStatus:Controller:{current.Role.ToString()}:{propertyName}:{value}", MockConsole.LogLevel.Debug);
                Manager.SendResponse("Android", "Station", $"DeviceStatus:Controller:{current.Role.ToString()}:{propertyName}:{value}");
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
            //Determine if the model should update and have the value sent to the NUC
            bool shouldUpdate;

            //Check if the entry exists
            if (baseStations.ContainsKey(serialNumber))
            {
                baseStations.TryGetValue(serialNumber, out var temp);
                if (temp == null)
                {
                    //Error has occurred
                    Logger.WriteLog($"VrStatus.UpdateBaseStation - A Base Station entry is invalid removing {serialNumber}",
                        MockConsole.LogLevel.Error);

                    //Clear the invalid entry
                    baseStations.Remove(serialNumber);
                    return;
                }
                shouldUpdate = temp.UpdateProperty(propertyName, value);
            }
            else
            {
                //Add a new base station entry
                MockConsole.WriteLine($"Found a new base station: {serialNumber}", MockConsole.LogLevel.Normal);
                VrBaseStation temp = new VrBaseStation(serialNumber);
                shouldUpdate = temp.UpdateProperty(propertyName, value);
                baseStations.Add(serialNumber, temp);
                UIUpdater.UpdateOpenVRStatus("baseStationAmount", baseStations.Count.ToString());
            }

            //Send a message to the NUC if necessary
            if (shouldUpdate || baseStationCount > (pollLimit * baseStations.Count))
            {
                baseStationCount = 0;
                int active = 0;
                foreach (var vrBaseStation in baseStations)
                {
                    if (vrBaseStation.Value.Tracking == DeviceStatus.Connected)
                    {
                        active++;
                    }
                }
                UIUpdater.UpdateOpenVRStatus("baseStationActive", active.ToString());
                
                //Send the active and total base station amounts instead of individual base station updates.
                MockConsole.WriteLine($"DeviceStatus:BaseStation:{active}:{baseStations.Count}", MockConsole.LogLevel.Debug);
                Manager.SendResponse("Android", "Station", $"DeviceStatus:BaseStation:{active}:{baseStations.Count}");
            } else
            {
                baseStationCount++;
            }
        }

        /// <summary>
        /// Reset all the statuses in the event of a headset drop or when the VR system is restarted.
        /// </summary>
        public void ResetStatuses()
        {
            //Reset headset
            HeadsetDescription = "";
            OpenVRStatus = DeviceStatus.Lost;
            UIUpdater.UpdateOpenVRStatus("headsetDescription", HeadsetDescription);
            UIUpdater.UpdateOpenVRStatus("headsetConnection", Enum.GetName(typeof(DeviceStatus), OpenVRStatus));

            //Update the tablet
            Manager.SendResponse("Android", "Station", $"DeviceStatus:Headset:tracking:{DeviceStatus.Lost.ToString()}");

            //Reset controllers
            foreach (var vrController in controllers)
            {
                vrController.Value.UpdateProperty("battery", 0);
                vrController.Value.UpdateProperty("tracking", DeviceStatus.Lost);

                string role = vrController.Value.Role == DeviceRole.Left ? "left" : "right";
                
                UIUpdater.UpdateOpenVRStatus($"{role}ControllerBattery", "0");
                UIUpdater.UpdateOpenVRStatus($"{role}ControllerConnection", Enum.GetName(typeof(DeviceStatus), DeviceStatus.Lost));

                //Update the tablet
                Manager.SendResponse("Android", "Station", $"DeviceStatus:Controller:{vrController.Value.Role.ToString()}:tracking:{DeviceStatus.Lost.ToString()}");
            }

            //Reset base stations
            foreach (var vrBaseStation in baseStations)
            {
                vrBaseStation.Value.UpdateProperty("tracking", DeviceStatus.Lost);
            }
            UIUpdater.UpdateOpenVRStatus("baseStationActive", "0");
            Manager.SendResponse("Android", "Station", $"DeviceStatus:BaseStation:0:{baseStations.Count}");
        }

        /// <summary>
        /// Re-send the current statuses of all VR devices. This is used when a tablet is connecting/reconnecting to the NUC.
        /// </summary>
        public void QueryStatues()
        {
            //Headset
            Manager.SendResponse("Android", "Station", $"DeviceStatus:Headset:tracking:{OpenVRStatus.ToString()}");

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
