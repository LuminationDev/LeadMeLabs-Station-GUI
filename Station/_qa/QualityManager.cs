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
            case "Network":
                message = _networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString());
                break;
            
            case "Windows":
                message = _windowChecks.GetLocalOsSettings();
                break;
            
            case "Software":
                message = _softwareChecks.GetSoftwareInformation();
                break;
            
            case "Config":
                message = _configChecks.GetLocalConfigurationDetails();
                break;
            
            case "StationDetails":
                message = _configChecks.GetLocalStationDetails();
                break;
            
            default:
                MockConsole.WriteLine($"Unknown type, QualityManager - DetermineCheck(): {type}.", MockConsole.LogLevel.Normal);
                break;
        }

        return message;
    }
}
