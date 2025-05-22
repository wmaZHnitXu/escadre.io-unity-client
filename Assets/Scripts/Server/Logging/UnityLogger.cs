// File: Scripts/Server/Logging/UnityLogger.cs
using UnityEngine; // Dependency on Unity API

namespace Core.Logging // Must match the namespace of the partial definition
{
    /// <summary>
    /// Unity-specific implementation for the partial Logger class.
    /// </summary>
    public static partial class Logger
    {
        // Implement the partial methods using UnityEngine.Debug
        public static partial void Log(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD // Optional: Log only in editor/dev builds
            Debug.Log(message);
            UnityLoggerDumper.Instance?.Add("LOG " + message);
#endif
        }

        public static partial void LogWarning(string message)
        {
            Debug.LogWarning(message);
            UnityLoggerDumper.Instance?.Add("WARN " + message);
        }

        public static partial void LogError(string message)
        {
            Debug.LogError(message);
             UnityLoggerDumper.Instance?.Add("ERR " + message);
        }
    }
}