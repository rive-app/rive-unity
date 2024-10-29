
namespace Rive
{
    /// <summary>
    /// Interface for logging debug messages.
    /// </summary>
    public interface IDebugLogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);

        void LogException(System.Exception exception);
    }
}