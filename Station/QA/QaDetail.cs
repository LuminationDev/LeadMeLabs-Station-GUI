using Newtonsoft.Json;

namespace Station.QA;

public class QaDetail
{
    [JsonProperty]
    private string? _value;
    [JsonProperty]
    private string? _message;
    [JsonProperty]
    private string _id;

    public QaDetail(string id)
    {
        _id = id;
    }
    
    public QaDetail(string id, string value)
    {
        _id = id;
        _value = value;
    }
    
    public QaDetail(string id, string value, string message)
    {
        _id = id;
        _value = value;
        _message = message;
    }

    public void SetValue(string value, string? message)
    {
        _message = message;
        _value = value;
    }
}