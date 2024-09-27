using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Station._config;
using Station.Components._enums;
using Station.Components._profiles;
using Station.Components._utils;
using Station.Core;

namespace Station.MVC.ViewModel;

public class ConfigurationViewModel : ObservableObject
{
    #region Input Instances
    // Helper method to get environment variable with a fallback to an empty string
    private string GetEnvironmentVariable(string key) =>
        Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process) ?? string.Empty;

    private string _encryptionKey;
    public string EncryptionKey
    {
        get => _encryptionKey;
        set => SetProperty(ref _encryptionKey, value);
    }

    private string _labLocation;
    public string LabLocation
    {
        get => _labLocation;
        set => SetProperty(ref _labLocation, value);
    }

    private string _stationId;
    public string StationId
    {
        get => _stationId;
        set => SetProperty(ref _stationId, value);
    }

    private string _room;
    public string Room
    {
        get => _room;
        set => SetProperty(ref _room, value);
    }

    private string _nucAddress;
    public string NucAddress
    {
        get => _nucAddress;
        set => SetProperty(ref _nucAddress, value);
    }

    private string _steamUserName;
    public string SteamUserName
    {
        get => _steamUserName;
        set => SetProperty(ref _steamUserName, value);
    }

    private string _steamPassword;
    public string SteamPassword
    {
        get => _steamPassword;
        set => SetProperty(ref _steamPassword, value);
    }
    
    // Helper method for property change notifications
    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        
        field = value;
        OnPropertyChanged(propertyName);
    }
    #endregion
    
    #region Input Dropdowns
    public ObservableCollection<StationMode> StationModes { get; }
    
    private StationMode _selectedMode = Helper.Mode;
    public StationMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            _selectedMode = value;
            OnPropertyChanged();
            IncludeHeadset = _selectedMode is StationMode.Pod or StationMode.VirtualReality;
        }
    }
    
    public ObservableCollection<Headset> HeadsetTypes { get; }
    private Headset? _selectedHeadset = VrProfile.GetHeadsetFromValue(
        Environment.GetEnvironmentVariable("HeadsetType", EnvironmentVariableTarget.Process) ?? string.Empty);
    public Headset? SelectedHeadset
    {
        get => _selectedHeadset;
        set
        {
            _selectedHeadset = value;
            OnPropertyChanged();
        }
    }
    #endregion
    
    #region Input Checkboxes
    private bool _includeSteamDetails;
    public bool IncludeSteamDetails
    {
        get => _includeSteamDetails;
        set
        {
            _includeSteamDetails = value;
            OnPropertyChanged();
        }
    }
    #endregion
    
    private bool _includeHeadset;
    public bool IncludeHeadset
    {
        get => _includeHeadset;
        set
        {
            _includeHeadset = value;
            OnPropertyChanged();
        }
    }
    
    // Commands
    public RelayCommand ConfigureSteamCmdCommand { get; }
    public RelayCommand SetConfigCommand { get; }

    // Constructor to initialize properties with environment variables
    public ConfigurationViewModel()
    {
        _encryptionKey = GetEnvironmentVariable("AppKey");
        _labLocation = GetEnvironmentVariable("LabLocation");
        _stationId = GetEnvironmentVariable("StationId");
        _room = GetEnvironmentVariable("room");
        _nucAddress = GetEnvironmentVariable("NucAddress");
        _steamUserName = GetEnvironmentVariable("SteamUserName");
        _steamPassword = GetEnvironmentVariable("SteamPassword");
        _includeHeadset = Helper.IsStationVrCompatible();
        
        //Populate the mode and headset list based on the available Enums
        StationModes = new ObservableCollection<StationMode>((StationMode[])Enum.GetValues(typeof(StationMode)));
        HeadsetTypes = new ObservableCollection<Headset>((Headset[])Enum.GetValues(typeof(Headset)));

        ConfigureSteamCmdCommand = new RelayCommand(_ => ConfigureSteamCmd());
        SetConfigCommand = new RelayCommand(_ => SetConfigFile());

        //Display the Steam details
        if (_steamUserName != string.Empty)
        {
            IncludeSteamDetails = true;
        }
    }
    
    /// <summary>
    /// Run a command window with the supplied Steam username and password to automatically configure SteamCMD.
    /// </summary>
    private void ConfigureSteamCmd()
    {
        //TODO configure steam cmd
    }
    
    /// <summary>
    /// Updates the configuration file with current settings by adding key-value pairs to a dictionary.
    /// Only non-null and non-empty values are added to the configuration.
    /// After updating the configuration, a message is displayed prompting the user to restart the program
    /// for the changes to take effect.
    /// </summary>
    private void SetConfigFile()
    {
        Dictionary<string, string> configValues = new Dictionary<string, string>();

        void AddIfNotNullOrEmpty(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                configValues[key] = value;
            }
        }

        // Adding values to the dictionary conditionally
        AddIfNotNullOrEmpty("AppKey", _encryptionKey);
        AddIfNotNullOrEmpty("LabLocation", _labLocation);
        AddIfNotNullOrEmpty("StationId", _stationId);
        AddIfNotNullOrEmpty("room", _room);
        AddIfNotNullOrEmpty("NucAddress", _nucAddress);
        AddIfNotNullOrEmpty("StationMode", Attributes.GetEnumValue(_selectedMode));
        AddIfNotNullOrEmpty("SteamUserName", _steamUserName);
        AddIfNotNullOrEmpty("SteamPassword", _steamPassword);
        if (_selectedHeadset != null)
        {
            AddIfNotNullOrEmpty("HeadsetType", Attributes.GetEnumValue(_selectedHeadset));
        }

        DotEnv.Update(configValues);
        ShowMessageBoxWithCallback();
    }
    
    /// <summary>
    /// Show a message box with Yes and No buttons prompting the User to restart the application.
    /// </summary>
    private void ShowMessageBoxWithCallback()
    {
        MessageBoxResult result = MessageBox.Show("Configuration file updated. Please restart the program for changes to take effect. Do you want to restart the application now?", 
            "Restart", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Question);

        // Process the result like a callback
        switch (result)
        {
            case MessageBoxResult.Yes:
                DotEnv.RestartApplication();
                break;
            case MessageBoxResult.No:
                break;
        }
    }
}
