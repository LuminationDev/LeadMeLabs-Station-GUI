using System;

namespace Station.Components._openvr;

public class OpenVrSystemException<TError> : Exception
{
    public readonly TError Error;

    public OpenVrSystemException() : base() { }
    public OpenVrSystemException(string message) : base(message) { }
    public OpenVrSystemException(string message, Exception inner) : base(message, inner) { }

    public OpenVrSystemException(string message, TError error) : this($"{message} ({error})")
    {
        Error = error;
    }
}
