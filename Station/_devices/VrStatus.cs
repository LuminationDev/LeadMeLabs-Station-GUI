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
    public class VrStatus
    {
        //Controller models stored by serial number
        private Dictionary<string, VrController> controllers = new();
        //Base Station models stored by serial number
        private Dictionary<string, VrBaseStation> baseStations = new();

        /// <summary>
        /// External software that is required to link the headset to SteamVR
        /// </summary>
        public DeviceStatus SoftwareStatus { private set; get; } //(Vive Pro (X) - Determined by Vive Logs)

        /// <summary>
        /// OpenVR's wrapper around SteamVR.
        /// </summary>
        public DeviceStatus OpenVRStatus { private set; get; } //Determined by OpenVR (should override viveStatus?)

        /// <summary>
        /// Update the VR headset status, there are two managers, one specific to the headset and
        /// OpenVR.
        /// </summary>
        /// <param name="manager">A VrManager enum of what application connection status is being updated.</param>
        /// <param name="status">A HMDStatus enum of the connection status (e.g. Connected, Lost</param>
        public void UpdateHeadset(VrManager manager, DeviceStatus status)
        {
            bool shouldUpdate = false;

            if (manager == VrManager.Software)
            {
                shouldUpdate = SoftwareStatus == status;
                SoftwareStatus = status;
            }
            else if (manager == VrManager.OpenVR)
            {
                shouldUpdate = OpenVRStatus == status;
                OpenVRStatus = status;

                //TODO Check if this will happen automatically?
                //TODO If status is Lost (Controllers/Base Stations are also lost)
            }

            //If just one of the trackers is lost then the headset is considered lost
            if (SoftwareStatus == DeviceStatus.Lost || OpenVRStatus == DeviceStatus.Lost)
            {
                status = DeviceStatus.Lost;
            }

            if (shouldUpdate)
            {
                SendStatusUpdate("Headset", "tracking", status);
            }
        }

        /// <summary>
        /// Update a VR controller.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="roll"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public void UpdateController(string serialNumber, DeviceRoll roll, string propertyName, object value)
        {
            bool shouldUpdate;

            //Check if the entry exists
            if (controllers.ContainsKey(serialNumber))
            {
                controllers.TryGetValue(serialNumber, out var temp);
                if (temp == null)
                {
                    //Error has occurred
                    Logger.WriteLog($"VrStatus.UpdateController - A Controller entry is invalid {serialNumber}",
                        MockConsole.LogLevel.Error);

                    //Clear the invalid entry
                    controllers.Remove(serialNumber);
                    return;
                }

                shouldUpdate = temp.UpdateProperty(propertyName, value);
            }
            else
            {
                //Add a new controller entry
                VrController temp = new VrController(serialNumber, roll);
                shouldUpdate = temp.UpdateProperty(propertyName, value);
                controllers.Add(serialNumber, temp);
            }

            if (shouldUpdate)
            {
                SendStatusUpdate("Controller", propertyName, value);
            }
        }

        /// <summary>
        /// Update a VR base station.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="tracking"></param>
        public void UpdateBaseStation(string serialNumber, string propertyName, object value)
        {
            bool shouldUpdate;

            //Check if the entry exists
            if (baseStations.ContainsKey(serialNumber))
            {
                baseStations.TryGetValue(serialNumber, out var temp);
                if (temp == null)
                {
                    //Error has occurred
                    Logger.WriteLog($"VrStatus.UpdateBaseStation - A Base Station entry is invalid {serialNumber}",
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
                VrBaseStation temp = new VrBaseStation(serialNumber);
                shouldUpdate = temp.UpdateProperty(propertyName, value);
                baseStations.Add(serialNumber, temp);
            }

            if (shouldUpdate)
            {
                SendStatusUpdate("BaseStation", propertyName, value);
            }
        }

        /// <summary>
        /// Send a message to the NUC with latest device statuses to be passed onto the tablet.
        /// </summary>
        public void SendStatusUpdate(string type, string property, object value)
        {
            Manager.SendResponse("Android", "Station", $"DeviceStatus:{type}:{property}:{value}");
        }
    }
}
