using Station._qa.checks;
namespace Station;

public class QualityManager
{
    public readonly NetworkChecks _networkChecks = new();
    public readonly WindowChecks _windowChecks = new();
    public readonly SoftwareChecks _softwareChecks = new();
    public readonly ConfigChecks _configChecks = new();
    public readonly SteamConfigChecks _steamConfigChecks = new();

    //TODO Add the QaCheck class in to handle the results
    public string? DetermineCheck(string type)
    {
        string? message = null;
        
        switch (type)
        {
            case "StationNetwork":
                message = _networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString());
                break;
            
            case "StationWindows":
                // message = _windowChecks.GetLocalOsSettings();
                break;
            
            case "StationConfig":
                message = _configChecks.GetLocalConfigurationDetails();
                break;
            
            case "StationDetails":
                message = _configChecks.GetLocalStationDetails();
                break;
            
            case "StationAll":
                message = _networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString()) + "::::";
                // message += _windowChecks.GetLocalOsSettings() + "::::";
                message += _configChecks.GetLocalConfigurationDetails();
                break;
            
            default:
                MockConsole.WriteLine($"Unknown type, QualityManager - DetermineCheck(): {type}.", MockConsole.LogLevel.Normal);
                break;
        }

        return message;
    }
}
