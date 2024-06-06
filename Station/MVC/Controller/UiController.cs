using LeadMeLabsLibrary;
using Station.Components._notification;
using Station.MVC.ViewModel;

namespace Station.MVC.Controller;

public static class UiController
{
    /// <summary>
    /// Update the Id of the Station.
    /// </summary>
    public static void UpdateStationId(string id)
    {
        if (MainViewModel.ViewModelManager.MainViewModel == null) return;
        MainViewModel.ViewModelManager.MainViewModel.Id = id;
    }
    
    /// <summary>
    /// Update the mode of the Station, currently this is limited to VR or non-VR controlled by a boolean.
    /// VR (true), non-VR (false)
    /// </summary>
    public static void UpdateStationMode(bool isVr)
    {
        MainViewModel.ViewModelManager.HomeViewModel.IsVr = isVr;
    }
    
    /// <summary>
    /// Updates software details in the MainViewModel based on the specified field.
    /// Supported fields include "ipAddress," "macAddress," "versionName," and "versionNumber."
    /// If the specified field is not recognized, a log message is written to the MockConsole.
    /// </summary>
    /// <param name="field">The field to update (e.g., "ipAddress").</param>
    /// <param name="value">The new value for the specified field.</param>
    public static void UpdateSoftwareDetails(string field, string value)
    {
        switch (field)
        {
            case "ipAddress":
                MainViewModel.ViewModelManager.HomeViewModel.IpAddress = value;
                break;
            
            case "macAddress":
                MainViewModel.ViewModelManager.HomeViewModel.MacAddress = value;
                break;
            
            case "versionName":
                MainViewModel.ViewModelManager.HomeViewModel.VersionName = value;
                break;
            
            case "versionNumber":
                MainViewModel.ViewModelManager.HomeViewModel.VersionNumber = value;
                break;
            
            default:
                MockConsole.WriteLine($"UIController - UpdateSoftwareDetails: Unknown field {field}; value {value}", 
                    Enums.LogLevel.Normal);
                break;
        }
    }
    
    /// <summary>
    /// Updates the status UI on the Home panel.
    /// </summary>
    public static void UpdateCurrentState(string status)
    {
        MainViewModel.ViewModelManager.HomeViewModel.CurrentState = status;
    }

    /// <summary>
    /// Update the overall power status (server etc..) of the Station.
    /// </summary>
    public static void UpdateStationPowerStatus(string status)
    {
        if (MainViewModel.ViewModelManager.MainViewModel == null) return;
        MainViewModel.ViewModelManager.MainViewModel.Status = status;
    }

    /// <summary>
    /// Update the headset description on the home page.
    /// </summary>
    public static void UpdateHeadsetDescription(string description)
    {
        if (MainViewModel.ViewModelManager.MainViewModel == null) return;
        MainViewModel.ViewModelManager.HomeViewModel.HeadsetDescription = description;
    }

    /// <summary>
    /// Update the VR connection status of openVr or the third party software managing the headset.
    /// </summary>
    public static void UpdateVrConnection(string type, string value)
    {
        switch (type)
        {
            case "openVr":
                MainViewModel.ViewModelManager.HomeViewModel.OpenVrConnection = value;
                break;
            
            case "thirdParty":
                MainViewModel.ViewModelManager.HomeViewModel.ThirdPartyConnection = value;
                break;
            
            default:
                MockConsole.WriteLine($"MainController - UpdateVrConnection: Unknown type {type}");
                break;
        }
    }
    
    /// <summary>
    /// Update the process name or status UI on the home page.
    /// </summary>
    /// <param name="key">A string of the text to update (processName, processStatus or reset)</param>
    /// <param name="value">A string to be displayed to the user</param>
    public static void UpdateProcessMessages(string key, string? value = null)
    {
        switch (key)
        {
            case "processName":
                MainViewModel.ViewModelManager.HomeViewModel.ProcessName = value;
                break;
            
            case "processStatus":
                MainViewModel.ViewModelManager.HomeViewModel.ProcessStatus = value;
                break;
            
            case "reset":
                MainViewModel.ViewModelManager.HomeViewModel.ProcessName = "No active process";
                MainViewModel.ViewModelManager.HomeViewModel.ProcessStatus = "Waiting";
                break;
        }
    }
    
    /// <summary>
    /// Update the VR icons on the home page.
    /// </summary>
    public static void UpdateVrUi(string device, string value)
    {
        switch (device)
        {
            case "headset":
                MainViewModel.ViewModelManager.HomeViewModel.HeadsetColor = value;
                break;
            
            case "leftController":
                MainViewModel.ViewModelManager.HomeViewModel.LeftControllerColor = value;
                break;
            
            case "rightController":
                MainViewModel.ViewModelManager.HomeViewModel.RightControllerColor = value;
                break;
            
            case "baseStation":
                MainViewModel.ViewModelManager.HomeViewModel.BaseStationColor = value;
                break;
            
            default:
                MockConsole.WriteLine($"MainController - UpdateVrUi: Unknown device {device}");
                break;
        }
    }
    
    /// <summary>
    /// Update the battery icons on the home page
    /// </summary>
    public static void UpdateVrBatteryUi(string device, int value)
    {
        switch (device)
        {
            case "leftController":
                MainViewModel.ViewModelManager.HomeViewModel.LeftBatteryIndex = value;
                break;
            
            case "rightController":
                MainViewModel.ViewModelManager.HomeViewModel.RightBatteryIndex = value;
                break;
            
            default:
                MockConsole.WriteLine($"MainController - UpdateVrBatteryUi: Unknown device {device}");
                break;
        }
    }
}
