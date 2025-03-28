using System.Collections.Generic;

namespace Station.QA.checks;

public class StationConnectionChecks
{
    private List<QaCheck> _qaChecks = new();
    public List<QaCheck> RunQa(string labType)
    {
        _qaChecks = new List<QaCheck>();
        QaCheck qaCheck = new QaCheck("station_is_connected");
        qaCheck.SetPassed(null); // if we have request for the check, then we're connected
        _qaChecks.Add(qaCheck);
        return _qaChecks;
    }
}
