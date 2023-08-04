using System;

namespace Station
{
    public class OpenVRSystemException<TError> : Exception
    {
        public readonly TError Error;

        public OpenVRSystemException() : base() { }
        public OpenVRSystemException(string message) : base(message) { }
        public OpenVRSystemException(string message, Exception inner) : base(message, inner) { }

        public OpenVRSystemException(string message, TError error) : this($"{message} ({error})")
        {
            Error = error;
        }
    }
}
