using System;
using System.Linq;
using LeadMeLabsLibrary;
using Station.Components._interfaces;
using Station.Components._notification;
using Station.Components._profiles;
using Station.Components._profiles._headsets;
using Station.MVC.Controller;

namespace Station.Components._models;

public class VrBaseStation
{
    private readonly string _serialNumber;

    private bool _firmwareUpdateRequired;
    
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
            MockConsole.WriteLine($"VrBaseStation {_serialNumber} tracking updated to {value} from {_tracking}", Enums.LogLevel.Verbose);
            _tracking = value;
            
            OnTrackingChanged(value.ToString());
        }
        get => _tracking;
    }

    public event EventHandler<GenericEventArgs<string>>? TrackingChanged;
    protected virtual void OnTrackingChanged(string newValue)
    {
        //Get the current active base stations
        int active =
            Statuses.baseStations.Count(vrBaseStation => vrBaseStation.Value.Tracking == DeviceStatus.Connected);

        string message = $"BaseStation:{active}:{Statuses.baseStations.Count}";
        MockConsole.WriteLine($"DeviceStatus:{message}", Enums.LogLevel.Debug);

        TrackingChanged?.Invoke(this, new GenericEventArgs<string>(message));
    }
    #endregion

    public VrBaseStation(string serialNumber)
    {
        this._serialNumber = serialNumber;
        
        // Safe cast for potential vr profile
        VrProfile? vrProfile = Profile.CastToType<VrProfile>(SessionController.StationProfile);
        if (vrProfile?.VrHeadset == null) return;

        TrackingChanged += vrProfile.VrHeadset.GetStatusManager().HandleValueChanged;
    }

    /// <summary>
    /// Updates the specified property of the VR controller based on the provided property name and value.
    /// </summary>
    /// <param name="propertyName">The name of the property to update. Accepted values: "tracking".</param>
    /// <param name="value">The value to set for the specified property.</param>
    /// <returns>A bool representing if the Station send an update, this should only be true if values changed.</returns>
    public void UpdateProperty(string propertyName, object value)
    {
        switch (propertyName.ToLower())
        {
            case "tracking":
                UpdateProperty(value, (DeviceStatus newValue) => Tracking = newValue,
                    "Invalid tracking value");
                break;
            case "firmware_update_required":
                _firmwareUpdateRequired = (bool) value;
                break;

            default:
                MockConsole.WriteLine($"VrBaseStation.UpdateProperty - Invalid property name: {propertyName}",
                    Enums.LogLevel.Error);
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
                Enums.LogLevel.Info);
        }
    }
}
