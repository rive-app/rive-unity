using System;

namespace Rive
{
    /// <summary>
    /// What went wrong, for a <see cref="RiveException"/>.
    /// </summary>
    public enum RiveErrorCode
    {
        LoadFailed = 0,
        ArtboardNotFound = 1,
        StateMachineNotFound = 2,
        ViewModelNotFound = 3,
        ViewModelInstanceNotFound = 4,
        PropertyNotFound = 5,
        TypeMismatch = 6,
        IndexOutOfRange = 7,
        ResourceDisposed = 8,
    }

    /// <summary>
    /// An error from Rive, with a code you can check instead of the message.
    /// </summary>
    public class RiveException : Exception
    {
        public RiveErrorCode Code { get; }

        public RiveException(RiveErrorCode code, string message) : base(message)
        {
            Code = code;
        }
    }
}
