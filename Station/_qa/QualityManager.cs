using Station._qa.checks;
namespace Station;

public class QualityManager
{
    public readonly NetworkChecks networkChecks = new();
    public readonly ImvrChecks imvrChecks = new();
    public readonly WindowChecks windowChecks = new();
    public readonly SoftwareChecks softwareChecks = new();
    public readonly ConfigChecks configChecks = new();
    public readonly SteamConfigChecks steamConfigChecks = new();
    public readonly StationConnectionChecks stationConnectionChecks = new();
}
