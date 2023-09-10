namespace Station._qa;

public class QaCheck
{
    private bool? _passedCheck = null;
    private string? _message = null;
    private string _checkId;

    public QaCheck(string checkId)
    {
        this._checkId = checkId;
    }

    public void SetFailed(string message)
    {
        this._passedCheck = false;
        this._message = message;
    }

    public void SetPassed(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            this._message = message;
        }

        this._passedCheck = true;
    }

    public bool GetPassedCheck()
    {
        return _passedCheck ?? false;
    }
}