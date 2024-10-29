using System;
using System.Collections.Generic;

namespace Rive.Tests.Utils
{
    /// <summary>
    /// A mock logger for use in tests.
    /// </summary>
    public class MockLogger : IDebugLogger
    {
        public List<string> LoggedMessages { get; } = new List<string>();
        public List<string> LoggedWarnings { get; } = new List<string>();
        public List<string> LoggedErrors { get; } = new List<string>();

        public List<Exception> LoggedExceptions { get; } = new List<Exception>();

        public void Log(string message) => LoggedMessages.Add(message);
        public void LogWarning(string message) => LoggedWarnings.Add(message);
        public void LogError(string message) => LoggedErrors.Add(message);

        public void LogException(Exception exception) => LoggedExceptions.Add(exception);

        public bool LoggedMessagesContains(string message) => LoggedMessages.Exists(m => m.Contains(message));

        public bool LoggedWarningsContains(string message) => LoggedWarnings.Exists(m => m.Contains(message));

        public bool LoggedErrorsContains(string message) => LoggedErrors.Exists(m => m.Contains(message));

        public bool LoggedExceptionsContains(string message) => LoggedExceptions.Exists(e => e.Message.Contains(message));

        public bool AnyLogTypeContains(string message) => LoggedMessagesContains(message) || LoggedWarningsContains(message) || LoggedErrorsContains(message) || LoggedExceptionsContains(message);
    }
}