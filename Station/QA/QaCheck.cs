using Newtonsoft.Json;

namespace Station.QA;

public class QaCheck
{
    [JsonProperty]
    private string? _passedStatus;
    [JsonProperty]
    private string? _message;
    [JsonProperty]
    private string _id;

    public QaCheck(string id)
    {
        this._id = id;
    }
    
    public void SetDetail(string message)
    {
        this._passedStatus = "detail";
        this._message = message;
    }

    public void SetFailed(string message)
    {
        this._passedStatus = "failed";
        this._message = message;
    }
    
    public void SetWarning(string message)
    {
        this._passedStatus = "warning";
        this._message = message;
    }
    
    public void SetNeedsConfirmation(string message)
    {
        this._passedStatus = "needs_confirmation";
        this._message = message;
    }

    public void SetPassed(string? message)
    {
        this._message = message;
        this._passedStatus = "passed";
    }

    public bool GetPassedCheck()
    {
        return _passedStatus != null && _passedStatus.Equals("passed");
    }
    
    public bool PassedStatusNotSet()
    {
        return _passedStatus == null;
    }
    
    // Methods used for UI
    [JsonIgnore]
    public string Id
    {
        get => _id;
        set
        {
            _id = value;
        }
    }
}
