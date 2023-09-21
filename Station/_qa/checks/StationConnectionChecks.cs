using System.Collections.Generic;

namespace Station._qa.checks;

public class StationConnectionChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa()
    {
        QaCheck qaCheck = new QaCheck("station_is_connected");
        qaCheck.SetPassed(null); // if we have request for the check, then we're connected
        _qaChecks.Add(qaCheck);
        return _qaChecks;
    }
}
