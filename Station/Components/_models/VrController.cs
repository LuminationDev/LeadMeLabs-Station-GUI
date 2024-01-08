using System;
using Station.Components._notification;
using Station.MVC.Controller;

namespace Station.Components._models;

public enum DeviceRole
{
    Left,
    Right
}

public class VrController
{
    //The role is set at creation and will not change.
    public DeviceRole Role { get; }
    private readonly string _serialNumber;
    private bool _firmwareUpdateRequired = false;

    public bool FirmwareUpdateRequired()
    {
        return _firmwareUpdateRequired;
    }

    #region Observers
    //Tracking status observer
    private DeviceStatus _tracking = DeviceStatus.Off;
    public DeviceStatus Tracking
    {
        private set
        {
            if (_tracking == value) return;

            OnTrackingChanged(value.ToString());
            MockConsole.WriteLine($"VrController {_serialNumber} tracking updated to {value} from {_tracking}",
                        MockConsole.LogLevel.Verbose);

            UIController.UpdateVrUi(
                Role == DeviceRole.Left ? "leftController" : "rightController", 
                Enum.GetName(typeof(DeviceStatus), value) ?? "Lost"
            );

            // Set the battery to 0 if it has lost connection
            if (value == DeviceStatus.Lost)
            {
                Battery = 0;
                UIController.UpdateVrBatteryUi(Role == DeviceRole.Left ? "leftController" : "rightController", 0);
            }

            _tracking = value;
        }
        get => _tracking;
    }

    public event EventHandler<GenericEventArgs<string>>? TrackingChanged;
    protected virtual void OnTrackingChanged(string newValue)
    {
        string message = $"Controller:{Role}:tracking:{newValue}";
        TrackingChanged?.Invoke(this, new GenericEventArgs<string>(message));
    }

    //Battery level observer
    private int _battery = 0;
    public int Battery
    {
        private set
        {
            if (_battery == value) return;

            OnBatteryChanged(value.ToString());
            MockConsole.WriteLine($"VrController {_serialNumber} battery updated to {value}% from {_battery}%",
                        MockConsole.LogLevel.Verbose);

            UIController.UpdateVrBatteryUi(Role == DeviceRole.Left ? "leftController" : "rightController", value);
            _battery = value;
        }
        get => _battery;
    }

    public event EventHandler<GenericEventArgs<string>>? BatteryChanged;
    protected virtual void OnBatteryChanged(string newValue)
    {
        string message = $"Controller:{Role}:battery:{newValue}";
        BatteryChanged?.Invoke(this, new GenericEventArgs<string>(message));
    }
    #endregion

    public VrController(string serialNumber, DeviceRole role) 
    { 
        this._serialNumber = serialNumber;
        this.Role = role;

        //Implement Observer Pattern
        if (SessionController.VrHeadset == null) return;
        
        BatteryChanged += SessionController.VrHeadset.GetStatusManager().HandleValueChanged;
        TrackingChanged += SessionController.VrHeadset.GetStatusManager().HandleValueChanged;
    }

    /// <summary>
    /// Updates the specified property of the VR controller based on the provided property name and value.
    /// </summary>
    /// <param name="propertyName">The name of the property to update. Accepted values: "battery", "tracking".</param>
    /// <param name="value">The value to set for the specified property.</param>
    /// <returns>A bool representing if the Station send an update, this should only be true if values changed.</returns>
    public void UpdateProperty(string propertyName, object value)
    {
        switch (propertyName.ToLower())
        {
            case "battery":
                UpdateProperty(value, (int newValue) => Battery = newValue,
                    "Invalid battery value");
                break;

            case "tracking":
                UpdateProperty(value, (DeviceStatus newValue) => Tracking = newValue,
                    "Invalid tracking value");
                break;
            
            case "firmware_update_required":
                this._firmwareUpdateRequired = (bool) value;
                break;

            default:
                MockConsole.WriteLine($"VrController.UpdateProperty - Invalid property name: {propertyName}",
                    MockConsole.LogLevel.Error);
                break;
        }
    }

    /// <summary>
    /// Updates a property with a new value of a specified type and handles errors if the value is of an invalid type.
    /// </summary>
    /// <typeparam name="T">The type of the property to update.</typeparam>
    /// <param name="newValue">The new value to assign to the property.</param>
    /// <param name="updateAction">The action to perform to update the property with the new value.</param>
    /// <param name="errorMsg">The error message to display if the new value is of an invalid type.</param>
    private void UpdateProperty<T>(object newValue, Action<T> updateAction, string errorMsg)
    {
        if (newValue is T typedValue)
        {
            updateAction(typedValue);
        }
        else
        {
            MockConsole.WriteLine($"VrController.UpdateProperty - {errorMsg}: {newValue}",
                MockConsole.LogLevel.Error);
        }
    }
}
