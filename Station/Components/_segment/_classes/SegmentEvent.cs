using System.Collections.Generic;
using Station.Components._segment._interfaces;

namespace Station.Components._segment._classes;

public class SegmentEvent: IEventDetails
{
    private readonly string _sessionId;
    private readonly string _key;
    private readonly string _classification;

    protected SegmentEvent(string sessionId, string key, string classification)
    {
        _sessionId = sessionId;
        _key = key;
        _classification = classification;
    }
    
    public string GetSessionId()
    {
        return _sessionId;
    }
    
    public string GetEvent()
    {
        return _key;
    }

    public string GetClassification()
    {
        return _classification;
    }

    public virtual Dictionary<string, object> ToPropertiesDictionary()
    {
        return new Dictionary<string, object>();
    }
}
