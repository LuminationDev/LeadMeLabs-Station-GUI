using System;
using System.Windows;

namespace Station
{
    public enum DeviceRole
    {
        Left,
        Right
    }

    public class VrController
    {
        private readonly string serialNumber;
        
        public DeviceRole? Role { private set; get; }
        public int Battery { private set; get; } = 0;
        public DeviceStatus Tracking { private set; get; } = DeviceStatus.Lost;

        public VrController(string serialNumber, DeviceRole? role) 
        { 
            this.serialNumber = serialNumber;
            this.Role = role;
        }

        /// <summary>
        /// Updates the specified property of the VR controller based on the provided property name and value.
        /// </summary>
        /// <param name="propertyName">The name of the property to update. Accepted values: "battery", "tracking".</param>
        /// <param name="value">The value to set for the specified property.</param>
        /// <returns>A bool representing if the Station send an update, this should only be true if values changed.</returns>
        public bool UpdateProperty(string propertyName, object value)
        {
            bool shouldUpdate = false;

            switch (propertyName.ToLower())
            {
                case "battery":
                    if (value is int batteryValue)
                    {
                        shouldUpdate = Battery != batteryValue;

                        Battery = batteryValue;
                        MockConsole.WriteLine($"VrController {serialNumber} battery updated to {Battery}% from {batteryValue}%",
                            MockConsole.LogLevel.Verbose);
                        
                        UIUpdater.UpdateOpenVRStatus(Role == DeviceRole.Left ? "leftControllerBattery" : "rightControllerBattery", 
                            Battery.ToString() ?? "0");
                    }
                    else
                    {
                        MockConsole.WriteLine($"VrController.UpdateProperty - Invalid battery value: {value}",
                           MockConsole.LogLevel.Error);
                    }
                    break;

                case "tracking":
                    if (value is DeviceStatus trackingValue)
                    {
                        shouldUpdate = Tracking != trackingValue;

                        Tracking = trackingValue;
                        MockConsole.WriteLine($"VrController {serialNumber} tracking updated to {Tracking} from {trackingValue}", 
                            MockConsole.LogLevel.Verbose);
                        
                        UIUpdater.UpdateOpenVRStatus(Role == DeviceRole.Left ? "leftControllerConnection" : "rightControllerConnection", 
                            Enum.GetName(typeof(DeviceStatus), Tracking) ?? "Lost");

                        // Set the battery to 0 if it has lost connection
                        if (Tracking == DeviceStatus.Lost)
                        {
                            Battery = 0;
                            UIUpdater.UpdateOpenVRStatus(Role == DeviceRole.Left ? "leftControllerBattery" : "rightControllerBattery", 
                                Battery.ToString() ?? "0");
                        }
                    }
                    else
                    {
                        MockConsole.WriteLine($"VrController.UpdateProperty - Invalid tracking value: {value}",
                            MockConsole.LogLevel.Error);
                    }
                    break;

                default:
                    MockConsole.WriteLine($"VrController.UpdateProperty - Invalid property name: {propertyName}",
                            MockConsole.LogLevel.Error);
                    break;
            }

            return shouldUpdate;
        }
    }
}
