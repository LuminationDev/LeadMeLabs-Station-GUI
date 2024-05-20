using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Station.Components._managers;
using Station.Components._utils;
using Station.Core;
using Station.MVC.Controller;

namespace Station.MVC.ViewModel;

public class HomeViewModel : ObservableObject
{
    //TODO put this somewhere
    private void RestartVr()
    {
        new Task(() =>
        {
            JObject message = new JObject
            {
                { "action", "SoftwareState" },
                { "value", "Shutting down VR processes" }
            };
            ScheduledTaskQueue.EnqueueTask(() => SessionController.PassStationMessage(message), TimeSpan.FromSeconds(1));
            _ = WrapperManager.RestartVrProcesses();
        }).Start();
    }
    
    #region VirtualRealityStatus

    private bool? _isVr = false;
    public bool? IsVr
    {
        get => _isVr;
        set
        {
            if (_isVr == value) return;
            _isVr = value;
            OnPropertyChanged();
        }
    }
    
    private const string Connected = "#00FF00";
    private const string Off = "#C3C8D8";
    private const string Lost = "#c42d2d";
    
    private string _headsetColor = "#C3C8D8";
    public string HeadsetColor
    {
        get => _headsetColor;
        set
        {
            string color = value switch
            {
                "Connected" => Connected,
                "Off" => Off,
                "Lost" => Lost,
                _ => Off
            };

            if (_headsetColor == color) return;
            
            _headsetColor = color;
            OnPropertyChanged();
        }
    }
    
    private string _leftControllerColor = "#C3C8D8";
    public string LeftControllerColor
    {
        get => _leftControllerColor;
        set
        {
            string color = value switch
            {
                "Connected" => Connected,
                "Off" => Off,
                "Lost" => Lost,
                _ => Off
            };
            
            if (_leftControllerColor == color) return;
            
            _leftControllerColor = color;
            OnPropertyChanged();
        }
    }

    private int _leftBatteryIndex = 0;
    public int LeftBatteryIndex
    {
        get => _leftBatteryIndex;
        set
        {
            if (_leftBatteryIndex == value) return;
            
            _leftBatteryIndex = value;
            OnPropertyChanged();
        }
    }
    
    private string _rightControllerColor = "#C3C8D8";
    public string RightControllerColor
    {
        get => _rightControllerColor;
        set
        {
            string color = value switch
            {
                "Connected" => Connected,
                "Off" => Off,
                "Lost" => Lost,
                _ => Off
            };
            
            if (_rightControllerColor == color) return;
            
            _rightControllerColor = color;
            OnPropertyChanged();
        }
    }
    
    private int _rightBatteryIndex = 0;
    public int RightBatteryIndex
    {
        get => _rightBatteryIndex;
        set
        {
            if (_rightBatteryIndex == value) return;
            
            _rightBatteryIndex = value;
            OnPropertyChanged();
        }
    }

    private string _baseStationColor = "#C3C8D8";
    public string BaseStationColor
    {
        get => _baseStationColor;
        set
        {
            string color = value switch
            {
                "Connected" => Connected,
                "Off" => Off,
                "Lost" => Lost,
                _ => Off
            };
            
            if (_baseStationColor == color) return;
            
            _baseStationColor = color;
            OnPropertyChanged();
        }
    }
    
    private string? _headsetDescription = "Searching..";
    public string? HeadsetDescription
    {
        get => _headsetDescription;
        set
        {
            if (_headsetDescription == value) return;
            _headsetDescription = value;
            OnPropertyChanged();
        }
    }
    
    private string? _thirdPartyConnection = "Off";
    public string? ThirdPartyConnection
    {
        get => _thirdPartyConnection;
        set
        {
            if (_thirdPartyConnection == value) return;
            _thirdPartyConnection = value;
            OnPropertyChanged();
        }
    }

    private string? _openVrConnection = "Off";
    public string? OpenVrConnection
    {
        get => _openVrConnection;
        set
        {
            if (_openVrConnection == value) return;
            _openVrConnection = value;
            OnPropertyChanged();
        }
    }
    #endregion

    #region SoftwareDetails
    private string? _versionNumber;
    public string? VersionNumber
    {
        get => _versionNumber;
        set
        {
            _versionNumber = value;
            OnPropertyChanged();
        }
    }
    
    private string? _versionName;
    public string? VersionName
    {
        get => _versionName;
        set
        {
            _versionName = value;
            OnPropertyChanged();
        }
    }
    
    private string? _processName = "No active process";
    public string? ProcessName
    {
        get => _processName;
        set
        {
            _processName = value;
            OnPropertyChanged();
        }
    }
    
    private string? _processStatus = "Waiting";
    public string? ProcessStatus
    {
        get => _processStatus;
        set
        {
            _processStatus = value;
            OnPropertyChanged();
        }
    }
    #endregion
    
    #region SoftwareInformation
    private string _currentState = "";
    public string CurrentState
    {
        get => _currentState;
        set
        {
            _currentState = value;
            OnPropertyChanged();
        }
    }
    
    private string _ipAddress = "";
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            _ipAddress = value;
            OnPropertyChanged();
        }
    }
    
    private string _macAddress = "";
    public string MacAddress
    {
        get => _macAddress;
        set
        {
            _macAddress = value;
            OnPropertyChanged();
        }
    }
    
    private string _systemUpTime = "";
    public string SystemUpTime
    {
        get => _systemUpTime;
        set
        {
            _systemUpTime = value;
            OnPropertyChanged();
        }
    }
    #endregion
}