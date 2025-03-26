using System;
using System.Collections.Generic;
using Station.Components._segment._interfaces;

namespace Station.Components._segment._classes;

public class SegmentStationEvent: SegmentEvent
{
    private readonly int _stationId;
    
    public SegmentStationEvent(string key) : base(Segment.GetSessionId(), key, SegmentConstants.EventTypeStation)
    {
        _stationId = int.Parse(Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "0");
    }
    
    public int GetStationId()
    {
        return _stationId;
    }
    
    public new Dictionary<string, object> ToPropertiesDictionary()
    {
        var properties = new Dictionary<string, object>
        {
            { "sessionId", GetSessionId() },
            { "stationId", _stationId },
            { "classification", GetClassification()}
        };

        return properties;
    }
}
