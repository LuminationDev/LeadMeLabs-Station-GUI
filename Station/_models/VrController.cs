using System;

namespace Station
{
    public enum DeviceRoll
    {
        Left,
        Right
    }

    public class VrController
    {
        private readonly string serialNumber;
        private readonly DeviceRoll roll;
        public int Battery { set; get; }
        public DeviceStatus Tracking { set; get; }

        public VrController(string serialNumber, DeviceRoll roll) 
        { 
            this.serialNumber = serialNumber;
            this.roll = roll;
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
                        shouldUpdate = Battery == batteryValue;

                        Battery = batteryValue;
                        MockConsole.WriteLine($"VrController {serialNumber} battery updated to {Battery}%",
                            MockConsole.LogLevel.Debug);
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
                        shouldUpdate = Tracking == trackingValue;

                        Tracking = trackingValue;
                        MockConsole.WriteLine($"VrController {serialNumber} tracking updated to {Tracking}", 
                            MockConsole.LogLevel.Debug);
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
