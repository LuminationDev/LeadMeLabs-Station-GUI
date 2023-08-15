using System;

namespace Station
{
    public class VrBaseStation
    {
        private readonly string serialNumber;
        public DeviceStatus Tracking { private set; get; } = DeviceStatus.Lost;

        public VrBaseStation(string serialNumber)
        {
            this.serialNumber = serialNumber;
        }

        /// <summary>
        /// Updates the specified property of the VR controller based on the provided property name and value.
        /// </summary>
        /// <param name="propertyName">The name of the property to update. Accepted values: "tracking".</param>
        /// <param name="value">The value to set for the specified property.</param>
        /// <returns>A bool representing if the Station send an update, this should only be true if values changed.</returns>
        public bool UpdateProperty(string propertyName, object value)
        {
            bool shouldUpdate = false;

            switch (propertyName.ToLower())
            {
                case "tracking":
                    if (value is DeviceStatus trackingValue)
                    {
                        shouldUpdate = Tracking != trackingValue;

                        Tracking = trackingValue;
                        MockConsole.WriteLine($"VrBaseStation {serialNumber} tracking updated to {Tracking}", MockConsole.LogLevel.Verbose);
                    }
                    else
                    {
                        MockConsole.WriteLine($"VrBaseStation.UpdateProperty - Invalid tracking value: {value}", 
                            MockConsole.LogLevel.Error);
                    }
                    break;

                default:
                    MockConsole.WriteLine($"VrBaseStation.UpdateProperty - Invalid property name: {propertyName}",
                            MockConsole.LogLevel.Error);
                    break;
            }

            return shouldUpdate;
        }
    }
}
