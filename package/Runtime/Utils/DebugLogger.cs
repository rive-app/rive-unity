using System;
using UnityEngine;
namespace Rive.Utils
{
    /// <summary>
    /// A custom logger for Rive that logs messages to the Unity console.
    /// </summary>
    public class DebugLogger : IDebugLogger
    {
        private static readonly IDebugLogger _instance = new DebugLogger();

        private static IDebugLogger _customInstance;

        public static IDebugLogger Instance
        {
            get
            {
                if (_customInstance != null)
                {
                    return _customInstance;
                }
                return _instance;
            }
            set
            {
                _customInstance = value;
            }
        }

        public void Log(string message)
        {
            Debug.Log($"[Rive]: {message}");
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning($"[Rive]: {message}");
        }

        public void LogError(string message)
        {
            Debug.LogError($"[Rive]: {message}");
        }

        public void LogException(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}