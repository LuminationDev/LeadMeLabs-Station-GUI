using Newtonsoft.Json.Linq;

namespace Station.Components._models;

public class ExperienceDetails
{
    private string WrapperType { get; }
    private string Name { get; }
    private string Id { get; }
    private bool IsVr { get; }
    
    /// <summary>
    /// Contains specific information about the Experience's category and how the Tablet should handle it's launching
    /// and operation.
    /// </summary>
    private JObject? Subtype { get; }
    
    public ExperienceDetails(string wrapperType, string name, string id, bool isVr, JObject? subtype = null)
    {
       WrapperType = wrapperType;
       Name = name;
       Id = id;
       IsVr = isVr;
       Subtype = subtype;
    }
    
    public JObject ToJObject()
    {
        JObject obj = new()
        {
            ["WrapperType"] = WrapperType,
            ["Name"] = Name,
            ["Id"] = Id,
            ["IsVr"] = IsVr,
            ["Subtype"] = Subtype
        };
        return obj;
    }
}
