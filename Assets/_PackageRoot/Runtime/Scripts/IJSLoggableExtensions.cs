using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace com.ijs.logger
{
    /// <summary>
    /// Extension methods that give any <see cref="IIJSLoggable"/> instance a per-type logger
    /// without requiring the consumer to declare an <see cref="IJSLogger"/> field on every class.
    /// All methods are <c>[Conditional("USE_LOGS")]</c>, so calls disappear in builds that don't
    /// define <c>USE_LOGS</c> -- matching the cost profile of <see cref="IJSLogger"/>'s own API.
    /// </summary>
    public static class IJSLoggableExtensions
    {
        // -------- Plain logging --------

        [Conditional("USE_LOGS")]
        public static void Log(this IIJSLoggable self, string message,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null) return;
            IJSLoggerRegistry.For(self.GetType())
                .LogAt(LogType.Log, message, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        [Conditional("USE_LOGS")]
        public static void LogWarning(this IIJSLoggable self, string message,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null) return;
            IJSLoggerRegistry.For(self.GetType())
                .LogAt(LogType.Warning, message, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        [Conditional("USE_LOGS")]
        public static void LogError(this IIJSLoggable self, string message,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null) return;
            IJSLoggerRegistry.For(self.GetType())
                .LogAt(LogType.Error, message, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        [Conditional("USE_LOGS")]
        public static void LogException(this IIJSLoggable self, Exception exception,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null || exception == null) return;
            IJSLoggerRegistry.For(self.GetType())
                .PrintException(exception, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        // -------- Conditional / throttled --------

        [Conditional("USE_LOGS")]
        public static void LogIf(this IIJSLoggable self, bool condition, string message,
            LogType logType = LogType.Log,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null || !condition) return;
            IJSLoggerRegistry.For(self.GetType())
                .LogAt(logType, message, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        /// <summary>
        /// Lazy-evaluated conditional log. Neither <paramref name="condition"/> nor
        /// <paramref name="messageBuilder"/> is invoked when the channel/severity/instance is filtered.
        /// </summary>
        [Conditional("USE_LOGS")]
        public static void LogIf(this IIJSLoggable self, Func<bool> condition, Func<string> messageBuilder,
            LogType logType = LogType.Log,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null || condition == null || messageBuilder == null) return;
            IJSLoggerRegistry.For(self.GetType())
                .LogIf(condition, messageBuilder, logType, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        [Conditional("USE_LOGS")]
        public static void LogThrottled(this IIJSLoggable self, string message, float minIntervalSeconds,
            LogType logType = LogType.Log,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (self == null) return;
            IJSLoggerRegistry.For(self.GetType())
                .LogThrottled(message, minIntervalSeconds, logType, null, callerFilePath, callerLineNumber, callerMemberName);
        }

        // -------- Assertions --------

        public static LogAssert Assert(this IIJSLoggable self, bool condition, string message,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            var logger = self != null ? IJSLoggerRegistry.For(self.GetType()) : null;
            return logger != null
                ? logger.Assert(condition, message, callerFilePath, callerLineNumber, callerMemberName)
                : new LogAssert(null, condition, message, callerFilePath, callerLineNumber, callerMemberName);
        }
    }
}
