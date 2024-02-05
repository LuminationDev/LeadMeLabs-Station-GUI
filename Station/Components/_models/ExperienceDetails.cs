using Newtonsoft.Json.Linq;

namespace Station.Components._models;

public class ExperienceDetails
{
    public string WrapperType { get; }
    public string Name { get; }
    public string Id { get; }
    public bool IsVr { get; }

    public ExperienceDetails(string wrapperType, string name, string id, bool isVr)
    {
        WrapperType = wrapperType;
        Name = name;
        Id = id;
        IsVr = isVr;
    }

    public JObject ToJObject()
    {
        JObject obj = new()
        {
            ["WrapperType"] = WrapperType,
            ["Name"] = Name,
            ["Id"] = Id,
            ["IsVr"] = IsVr
        };
        return obj;
    }
}
