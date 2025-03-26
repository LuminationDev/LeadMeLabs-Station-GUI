using System;
using System.Collections.Generic;
using Station.Components._segment._interfaces;

namespace Station.Components._segment._classes;

public class SegmentExperienceEvent: SegmentEvent, IExperienceEventDetails
{
    private readonly int _stationId;
    private readonly string _name;
    private readonly string _id;
    private readonly string _type;
    private int _runtime;
    
    public SegmentExperienceEvent(string key, string name, string id, string type) : base(Segment.GetSessionId(), key, SegmentConstants.EventTypeExperience)
    {
        _stationId = int.Parse(Environment.GetEnvironmentVariable("StationId", EnvironmentVariableTarget.Process) ?? "0");
        _name = name;
        _id = id;
        _type = type;
    }

    public void SetRuntime(int runtime)
    {
        _runtime = runtime;
    }
    
    public int GetStationId()
    {
        return _stationId;
    }

    public string GetName()
    {
        return _name;
    }

    public string GetId()
    {
        return _id;
    }

    public new string GetType()
    {
        return _type;
    }

    public int GetRuntime()
    {
        return _runtime;
    }
    
    public new Dictionary<string, object> ToPropertiesDictionary()
    {
        var properties = new Dictionary<string, object>
        {
            { "sessionId", GetSessionId() },
            { "stationId", _stationId },
            { "name", _name },
            { "id", _id },
            { "type", _type },
            { "runtime", _runtime },
            { "classification", GetClassification()}
        };

        return properties;
    }
}
