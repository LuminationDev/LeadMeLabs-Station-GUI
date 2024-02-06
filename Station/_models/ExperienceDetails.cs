using Newtonsoft.Json.Linq;

namespace Station._models;

public class ExperienceDetails
{
    private string WrapperType { get; }
    private string Name { get; }
    private string Id { get; }
    private bool IsVr { get; }
    
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
