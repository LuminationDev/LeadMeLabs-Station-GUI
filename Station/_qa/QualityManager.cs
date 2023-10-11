using Newtonsoft.Json;
using Station._qa.checks;
namespace Station;

public class QualityManager
{
    public readonly NetworkChecks networkChecks = new();
    public readonly WindowChecks windowChecks = new();
    public readonly SoftwareChecks softwareChecks = new();
    public readonly ConfigChecks configChecks = new();
    public readonly SteamConfigChecks steamConfigChecks = new();
    public readonly StationConnectionChecks stationConnectionChecks = new();

    //TODO Add the QaCheck class in to handle the results
    public string? DetermineCheck(string type)
    {
        string? message = null;
        
        switch (type)
        {
            // case "StationNetwork":
            //     message = JsonConvert.SerializeObject(networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString()));
            //     break;
            
            case "StationWindows":
                message = JsonConvert.SerializeObject(windowChecks.RunQa());
                break;
            
            case "StationSoftware":
                message = JsonConvert.SerializeObject(softwareChecks.RunQa());
                break;
            
            case "StationConfig":
                message = JsonConvert.SerializeObject(configChecks.GetLocalStationDetails());
                break;
            
            case "StationAll":
                // message = networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString()) + "::::";
                // message += _windowChecks.GetLocalOsSettings() + "::::";
                break;
            
            default:
                MockConsole.WriteLine($"Unknown type, QualityManager - DetermineCheck(): {type}.", MockConsole.LogLevel.Normal);
                break;
        }

        return message;
    }
}
