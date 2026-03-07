using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

namespace com.ijs.logger
{
    /// <summary>
    /// Defines different logging channels/categories for filtering logs.
    /// </summary>
    public enum LogChannel
    {
        Default,      // Always enabled if USE_LOGS is defined
        Audio,
        Network,
        Physics,
        AI,
        UI,
        Gameplay,
        Performance,
        Animation,
        Input,
        Rendering,
        System
    }

    /// <summary>
    /// Defines where a channel should be active.
    /// </summary>
    public enum ChannelScope
    {
        EditorOnly,   // Only log in Unity Editor
        BuildOnly,    // Only log in builds (not editor)
        Both          // Log in both editor and builds
    }

    /// <summary>
    /// Handles rate limiting for log messages to prevent spam.
    /// </summary>
    public static class LogRateLimiter
    {
        private class LogEntry
        {
            public float LastLogTime;
            public int SuppressedCount;
        }

        private static readonly Dictionary<string, LogEntry> LogCache = new Dictionary<string, LogEntry>();

        /// <summary>
        /// Checks if a log should be displayed based on rate limiting.
        /// </summary>
        /// <param name="key">Unique key for this log message</param>
        /// <param name="rateLimitSeconds">Minimum seconds between logs</param>
        /// <returns>True if the log should be displayed, false if suppressed</returns>
        public static bool ShouldLog(string key, float rateLimitSeconds)
        {
            if (!LogCache.TryGetValue(key, out var entry))
            {
                LogCache[key] = new LogEntry { LastLogTime = Time.unscaledTime, SuppressedCount = 0 };
                return true;
            }

            if (Time.unscaledTime - entry.LastLogTime >= rateLimitSeconds)
            {
                entry.LastLogTime = Time.unscaledTime;
                entry.SuppressedCount = 0;
                return true;
            }

            entry.SuppressedCount++;
            return false;
        }

        /// <summary>
        /// Gets the number of suppressed logs for a given key.
        /// </summary>
        public static int GetSuppressedCount(string key)
        {
            return LogCache.TryGetValue(key, out var entry) ? entry.SuppressedCount : 0;
        }

        /// <summary>
        /// Clears all rate limiting data.
        /// </summary>
        public static void Clear()
        {
            LogCache.Clear();
        }
    }

    /// <summary>
    /// Provides scoped context for log messages using the disposable pattern.
    /// </summary>
    public class LogContext : IDisposable
    {
        private static readonly Stack<string> ContextStack = new Stack<string>();

        /// <summary>
        /// Gets the current context string formatted for log messages.
        /// </summary>
        public static string CurrentContext
        {
            get
            {
                if (ContextStack.Count == 0) return "";
                var contexts = new List<string>(ContextStack);
                contexts.Reverse();
                return $"[{string.Join(" > ", contexts)}] ";
            }
        }

        /// <summary>
        /// Creates a new log context scope.
        /// </summary>
        /// <param name="context">The context name</param>
        public LogContext(string context)
        {
            ContextStack.Push(context);
        }

        /// <summary>
        /// Removes this context from the stack when disposed.
        /// </summary>
        public void Dispose()
        {
            if (ContextStack.Count > 0)
                ContextStack.Pop();
        }

        /// <summary>
        /// Clears all contexts.
        /// </summary>
        public static void Clear()
        {
            ContextStack.Clear();
        }
    }

    /// <summary>
    /// Provides fluent assertion API with automatic logging and actions.
    /// </summary>
    public class LogAssert
    {
        private readonly IJSLogger _logger;
        private readonly bool _condition;
        private readonly string _message;

        internal LogAssert(IJSLogger logger, bool condition, string message)
        {
            _logger = logger;
            _condition = condition;
            _message = message;

            if (!condition)
            {
                _logger.PrintLog($"ASSERTION FAILED: {message}", LogType.Error);
            }
        }

        /// <summary>
        /// Executes a callback if the assertion fails.
        /// </summary>
        public LogAssert OnFailure(Action callback)
        {
            if (!_condition)
                callback?.Invoke();
            return this;
        }

        /// <summary>
        /// Breaks into the debugger if attached and assertion fails.
        /// </summary>
        public LogAssert BreakDebugger()
        {
            if (!_condition && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
            return this;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Pauses the Unity Editor if the assertion fails.
        /// </summary>
        public LogAssert PauseEditor()
        {
            if (!_condition)
                UnityEditor.EditorApplication.isPaused = true;
            return this;
        }
#endif
    }

    public class IJSLogger
    {
        private static readonly IJSLogger DisabledLogger = new IJSLogger(false);

        #if UNITY_EDITOR
        private const string UseLogs = "USE_LOGS";
        private static bool useLogs;

        /// <summary>
        /// Enables or disables the use of logs in the IJSLogger class.
        /// This method sets a scripting define symbol based on the enable parameter value.
        /// </summary>
        [MenuItem("IJS/Logger/Enable Logs")]
        private static void EnableUseLogs()
        {
            useLogs = true;
            OnUseLogsChanged();
        }

        /// <summary>
        /// The <c>DisableUseLogs</c> method disables the use of logs in the Unity application.
        /// </summary>
        [MenuItem("IJS/Logger/Disable Logs")]
        private static void DisableUseLogs()
        {
            useLogs = false;
            OnUseLogsChanged();
        }

        /// <summary>
        /// The <c>IJSLogger</c> class is a utility class that provides logging capabilities in Unity.
        /// It allows you to print log messages with customizable prefixes, colors, and log types.
        /// </summary>
        private static void OnUseLogsChanged()
        {
            UpdateScriptingDefineSymbols(UseLogs, useLogs);
        }


        /// <summary>
        /// Updates the scripting define symbols based on the given value and whether to add or remove it.
        /// </summary>
        /// <param name="val">The value to add or remove from the scripting defines symbols.</param>
        /// <param name="add">A boolean indicating whether to add or remove the given value.</param>
        private static void UpdateScriptingDefineSymbols(string val, bool add)
        {
#if UNITY_EDITOR
        var platform = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        var definesString = PlayerSettings.GetScriptingDefineSymbols(platform);
        var allDefines = definesString.Split(';').ToList();
        if (add)
        {
            if (!allDefines.Contains(val))
                allDefines.Add(val);
        }
        else
        {
            if (allDefines.Contains(val))
                allDefines.Remove(val);
        }
        PlayerSettings.SetScriptingDefineSymbols(platform, string.Join(";", allDefines.ToArray()));
#endif
        }
#endif

        private Color _logColor; // Color for log messages
        private readonly bool _isNoOpLogger; // Whether this instance is the shared no-op logger
        private bool _logsEnabled; // Whether or not to log
        private string _logPrefix; // Prefix for log messages
        private LogChannel _channel; // Channel for filtering logs

        private IJSLogger(bool logsEnabled)
        {
            _logColor = Color.white;
            _logPrefix = string.Empty;
            _logsEnabled = logsEnabled;
            _channel = LogChannel.Default;
            _isNoOpLogger = !logsEnabled;
        }

        /// <summary>
        /// Creates a logger instance only when <paramref name="logsEnabled"/> is true and <c>USE_LOGS</c> is defined.
        /// Returns a shared no-op logger when either condition is not met.
        /// Loggers returned in a disabled state cannot be enabled later; create a new logger if you need logging enabled.
        /// </summary>
        public static IJSLogger Create(string prefix = "", Color? color = null, bool logsEnabled = true, LogChannel channel = LogChannel.Default)
        {
            if (!ShouldCreateLogger(logsEnabled))
                return DisabledLogger;

            return new IJSLogger(prefix, color, logsEnabled, channel);
        }

        private static bool ShouldCreateLogger(bool logsEnabled)
        {
            if (!logsEnabled)
                return false;

#if USE_LOGS
            return true;
#else
            return false;
#endif
        }

        public IJSLogger(string prefix = "", Color? color = null, bool logsEnabled = true, LogChannel channel = LogChannel.Default)
        {
            _logColor = color ?? Color.white;
            _logPrefix = prefix;
            _logsEnabled = logsEnabled;
            _channel = channel;
            _isNoOpLogger = false;
        }

        /// <summary>
        /// Attempts to enable logging for this instance.
        /// Returns <c>false</c> if the logger is a no-op logger or has been disabled, because disabled loggers cannot be re-enabled.
        /// </summary>
        public bool EnableLogs()
        {
            if (_isNoOpLogger)
                return false;

            if (_logsEnabled)
                return true;

            return false;
        }

        /// <summary>
        /// Disables logging for this instance.
        /// Returns <c>true</c> when the state changes and <c>false</c> when the logger is already disabled.
        /// For no-op loggers, this method has no effect and returns <c>false</c>.
        /// </summary>
        public bool DisableLogs()
        {
            if (_isNoOpLogger) return false;
            if (!_logsEnabled) return false;

            _logsEnabled = false;
            return true;
        }

        [Obsolete("Use EnableLogs() or DisableLogs() instead. Disabled loggers cannot be re-enabled after creation.")]
        public void ToggleLogs(bool enable)
        {
            if (enable)
            {
                EnableLogs();
                return;
            }

            DisableLogs();
        }

        public void ModifyPrefix(string prefix)
        {
            if (_isNoOpLogger) return;
            _logPrefix = prefix;
        }

        public void ModifyColor(Color color)
        {
            if (_isNoOpLogger) return;
            _logColor = color;
        }

        /// <summary>
        /// Asserts a condition and logs an error if it fails.
        /// </summary>
        public LogAssert Assert(bool condition, string message)
        {
            return new LogAssert(this, condition, message);
        }

        /// <summary>
        /// Validates that an object is not null.
        /// </summary>
        public void ValidateNotNull(object obj, string paramName)
        {
            Assert(obj != null, $"{paramName} cannot be null");
        }

        /// <summary>
        /// Validates that a value is within a specified range.
        /// </summary>
        public void ValidateRange(float value, float min, float max, string paramName)
        {
            Assert(value >= min && value <= max, $"{paramName} must be between {min} and {max}, but was {value}");
        }

        /// <summary>
        /// Logs a message only if the condition is true.
        /// </summary>
        [Conditional("USE_LOGS")]
        public void LogIf(bool condition, string message, LogType logType = LogType.Log, GameObject go = null)
        {
            if (!_logsEnabled) return;
            if (condition)
                PrintLog(message, logType, go);
        }

        /// <summary>
        /// Logs a message only if the condition function returns true. Uses lazy evaluation.
        /// </summary>
        [Conditional("USE_LOGS")]
        public void LogIf(Func<bool> condition, Func<string> messageBuilder, LogType logType = LogType.Log, GameObject go = null)
        {
            if (!_logsEnabled) return;
            if (condition())
                PrintLog(messageBuilder(), logType, go);
        }

        /// <summary>
        /// Logs a message with rate limiting to prevent spam.
        /// </summary>
        [Conditional("USE_LOGS")]
        public void LogThrottled(string message, float minIntervalSeconds, LogType logType = LogType.Log, GameObject go = null)
        {
            if (!_logsEnabled) return;
            var key = $"{GetHashCode()}_{message}";
            if (LogRateLimiter.ShouldLog(key, minIntervalSeconds))
            {
                var suppressedCount = LogRateLimiter.GetSuppressedCount(key);
                var finalMessage = suppressedCount > 0
                    ? $"{message} (suppressed {suppressedCount}x)"
                    : message;
                PrintLog(finalMessage, logType, go);
            }
        }

        [Conditional("USE_LOGS")]
        public void PrintLog(string message, LogType logType = LogType.Log, GameObject go = null)
        {
            if (!_logsEnabled) return;

            // Check if channel is enabled
            if (!IJSLoggerSettings.IsChannelEnabled(_channel)) return;

            // Add context if any
            message = LogContext.CurrentContext + message;

            // Add prefix
            if (!string.IsNullOrEmpty(_logPrefix))
                message = $"{_logPrefix}:: {message}";

            Log(message, logType, go, _logColor);
        }

        [Conditional("USE_LOGS")]
        public static void Log(
            string message,
            LogType type = LogType.Log,
            GameObject go = null,
            Color? color = null)
        {
            color = color ?? Color.white;
#if UNITY_EDITOR
            var splitWords = message.Split();

            for (var i = 0; i < splitWords.Length; i++)
            {
                if (int.TryParse(splitWords[i], out _))
                {
                    splitWords[i] = $"<size=13><color=#FF214C>{splitWords[i]} </color></size>";
                }
            }

            var updatedText = string.Join(" ", splitWords);

            var formattedMessage =
                $"<size=11><i><b><color=#{(byte)(color.Value.r * 255f):X2}{(byte)(color.Value.g * 255f):X2}{(byte)(color.Value.b * 255f):X2}>{updatedText}</color></b></i></size>";

            switch (type)
            {
                case LogType.Error:
                    Debug.LogError(formattedMessage, go);
                    break;
                case LogType.Assert:
                    Debug.LogAssertion(formattedMessage, go);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(formattedMessage, go);
                    break;
                case LogType.Log:
                    Debug.Log(formattedMessage, go);
                    break;
                case LogType.Exception:
                    Debug.LogException(new Exception(formattedMessage), go);
                    break;
            }
#else
            var stackTrace = new StackTrace(true);
            var callerFrames = GetRelevantStackFrames(stackTrace);

            // Get the method two steps back in the call stack, effectively skipping logger methods
            var firstCallerFrame = callerFrames.ElementAtOrDefault(0);
            var secondCallerFrame = callerFrames.ElementAtOrDefault(1);

            var firstCallerClassName = firstCallerFrame?.GetMethod()?.DeclaringType?.Name ?? "UnknownClass";
            var firstCallerMethodName = firstCallerFrame?.GetMethod()?.Name ?? "UnknownMethod";

            var secondCallerClassName = secondCallerFrame?.GetMethod()?.DeclaringType?.Name ?? "UnknownClass";
            var secondCallerMethodName = secondCallerFrame?.GetMethod()?.Name ?? "UnknownMethod";

            string logMessage = message;

            // Append current class and method only if they are not unknown
            if (firstCallerClassName != "UnknownClass" && firstCallerMethodName != "UnknownMethod")
                logMessage += $" ⇒ F: {firstCallerClassName}.{firstCallerMethodName}";

            // Append previous class and method only if they are not unknown
            if (secondCallerClassName != "UnknownClass" && secondCallerMethodName != "UnknownMethod")
                logMessage += $", S: {secondCallerClassName}.{secondCallerMethodName}";

            Debug.Log($"\n\n{logMessage}\n");
#endif
        }


        private static List<StackFrame> GetRelevantStackFrames(StackTrace stackTrace)
        {
            var frames = new List<StackFrame>();
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var methodName = frame?.GetMethod()?.Name;
                var className = frame?.GetMethod()?.DeclaringType?.Name;
                if (methodName != "MoveNext"
                    && methodName != "InvokeMoveNext"
                    && !(methodName == "Start" && className == "AsyncVoidMethodBuilder")
                    && methodName != "PrintLog"
                    && methodName != "RunInternal"
                    && methodName != "InvokeAction"
                    && methodName != "FinishContinuations"
                    && methodName != "Invoke"
                    && methodName != "ExecuteTasks"
                    && methodName != "RunCallback"
                    && methodName != "Exec"
                    && !(className?.StartsWith("AsyncTaskMethodBuilder") ?? false)
                    && !(className == "ExecutionContext" && methodName == "Run")
                    && !(className == "SynchronizationContextAwaitTaskContinuation" && methodName == "Run")
                    && !(className == "AwaitTaskContinuation" && methodName == "InvokeAction")
                    && !(className == "MoveNextRunner" && methodName == "Run")
                    && className != "IJSLogger"
                    && className != "Tween"
                    && className != "Clickable"
                    && className != "TaskCompletionSource"
                    && className != "TweenManager"
                    && className != "UnitySynchronizationContext"
                    && className != "Task"
                    && className != "InternalEditorUtility"
                    && className != "DOTweenComponent"
                    && className != "Socket"
                    && className != "Task`1"
                    && className != "<>c"
                    && !(className == "WorkRequest" && methodName == "Invoke")) // Ignore specific methods and classes
                {
                    frames.Add(frame);
                }
            }
            return frames;
        }
    }
}
