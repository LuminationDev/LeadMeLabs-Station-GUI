using Station._qa.checks;
namespace Station;

public class QualityManager
{
    private readonly NetworkChecks _networkChecks = new();
    private readonly WindowChecks _windowChecks = new();
    private readonly SoftwareChecks _softwareChecks = new();
    private readonly ConfigChecks _configChecks = new();

    //TODO Add the QaCheck class in to handle the results
    public string? DetermineCheck(string type)
    {
        string? message = null;
        
        switch (type)
        {
            case "StationNetwork":
                message = "Network::::" + _networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString());
                break;
            
            case "StationWindows":
                message = "Windows::::" + _windowChecks.GetLocalOsSettings();
                break;
            
            case "StationSoftware":
                message = "Software::::" + _softwareChecks.GetSoftwareInformation();
                break;
            
            case "StationConfig":
                message = "Config::::" + _configChecks.GetLocalConfigurationDetails();
                break;
            
            case "StationDetails":
                message = _configChecks.GetLocalStationDetails();
                break;
            
            case "StationAll":
                message = _networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString()) + "::::";
                message += _windowChecks.GetLocalOsSettings() + "::::";
                message += _softwareChecks.GetSoftwareInformation() + "::::";
                message += _configChecks.GetLocalConfigurationDetails() + "::::";
                message += _configChecks.GetLocalStationDetails();
                break;
            
            default:
                MockConsole.WriteLine($"Unknown type, QualityManager - DetermineCheck(): {type}.", MockConsole.LogLevel.Normal);
                break;
        }

        return message;
    }
}
