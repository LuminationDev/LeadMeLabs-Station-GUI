using System.Collections.Generic;

namespace Station.Components._segment._interfaces;

public interface IEventDetails
{
    string GetSessionId();
    string GetEvent();
    string GetClassification();

    Dictionary<string, object> ToPropertiesDictionary();
}
