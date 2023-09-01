using Station._qa.checks;
namespace Station;

public class QualityManager
{
    private readonly NetworkChecks _networkChecks = new();
    private readonly WindowChecks _windowChecks = new();
    private readonly SoftwareChecks _softwareChecks = new();

    //TODO Add the QaCheck class in to handle the results
    public void DetermineCheck(string type)
    {
        switch (type)
        {
            case "Network":
                _networkChecks.GetNetworkInterfaceByIpAddress(Manager.localEndPoint.Address.ToString());
                break;
            
            case "Windows":
                _windowChecks.GetLocalOsSettings();
                break;
            
            case "Software":
                _softwareChecks.GetSoftwareInformation();
                break;
        }
    }
}
