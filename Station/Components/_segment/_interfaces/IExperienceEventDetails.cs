using System.Collections.Generic;

namespace Station.Components._segment._interfaces;

public interface IExperienceEventDetails: IEventDetails
{
    int GetStationId();
    string GetName();
    string GetId();
    string GetType();
    int GetRuntime();
}
