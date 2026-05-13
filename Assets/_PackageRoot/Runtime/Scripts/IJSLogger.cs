using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
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
    /// Thread-safe rate limiter for log messages with bounded memory usage.
    /// Uses a soft-LRU eviction policy: when the cache exceeds <see cref="MaxEntries"/>,
    /// the entries with the oldest <c>LastLogTime</c> are dropped down to <see cref="TrimTo"/>.
    /// </summary>
    public static class LogRateLimiter
    {
        private class LogEntry
        {
            public float LastLogTime;
            public int SuppressedCount;
        }

        private static readonly ConcurrentDictionary<string, LogEntry> LogCache =
            new ConcurrentDictionary<string, LogEntry>();

        /// <summary>Maximum number of distinct keys retained before eviction triggers.</summary>
        public static int MaxEntries = 4096;

        /// <summary>Target size after eviction. Must be &lt; <see cref="MaxEntries"/>.</summary>
        public static int TrimTo = 3072;

        private static int _trimInProgress;

        /// <summary>
        /// Checks if a log should be displayed based on rate limiting.
        /// </summary>
        /// <param name="key">Unique key for this log message</param>
        /// <param name="rateLimitSeconds">Minimum seconds between logs</param>
        /// <returns>True if the log should be displayed, false if suppressed</returns>
        public static bool ShouldLog(string key, float rateLimitSeconds)
        {
            if (key == null) return true;

            var now = SafeUnscaledTime();
            var entry = LogCache.GetOrAdd(key, _ => new LogEntry { LastLogTime = float.NegativeInfinity });

            lock (entry)
            {
                if (now - entry.LastLogTime >= rateLimitSeconds)
                {
                    entry.LastLogTime = now;
                    entry.SuppressedCount = 0;
                    MaybeTrim();
                    return true;
                }

                entry.SuppressedCount++;
                return false;
            }
        }

        /// <summary>
        /// Gets the number of suppressed logs for a given key.
        /// </summary>
        public static int GetSuppressedCount(string key)
        {
            if (key == null) return 0;
            return LogCache.TryGetValue(key, out var entry) ? entry.SuppressedCount : 0;
        }

        /// <summary>Clears all rate limiting data.</summary>
        public static void Clear() => LogCache.Clear();

        private static float SafeUnscaledTime()
        {
            // Time.unscaledTime can only be read from the main thread. If called from a
            // background thread we fall back to a process-relative time.
            try { return Time.unscaledTime; }
            catch { return (float)(DateTime.UtcNow - _processStart).TotalSeconds; }
        }

        private static readonly DateTime _processStart = DateTime.UtcNow;

        private static void MaybeTrim()
        {
            if (LogCache.Count <= MaxEntries) return;
            if (Interlocked.CompareExchange(ref _trimInProgress, 1, 0) != 0) return;
            try
            {
                var snapshot = LogCache.ToArray();
                var target = Math.Max(0, Math.Min(TrimTo, MaxEntries - 1));
                var toRemove = snapshot.Length - target;
                if (toRemove <= 0) return;

                var ordered = snapshot.OrderBy(kv => kv.Value.LastLogTime).Take(toRemove);
                foreach (var kv in ordered)
                    LogCache.TryRemove(kv.Key, out _);
            }
            finally
            {
                Interlocked.Exchange(ref _trimInProgress, 0);
            }
        }
    }

    /// <summary>
    /// Provides scoped context for log messages using the disposable pattern.
    /// Contexts are stored in an <see cref="AsyncLocal{T}"/> stack so they flow across
    /// <c>await</c> points and remain isolated between threads/tasks.
    /// </summary>
    public class LogContext : IDisposable
    {
        private static readonly AsyncLocal<Stack<string>> Local = new AsyncLocal<Stack<string>>();

        private static Stack<string> Current
        {
            get
            {
                var s = Local.Value;
                if (s == null)
                {
                    s = new Stack<string>();
                    Local.Value = s;
                }
                return s;
            }
        }

        /// <summary>
        /// Gets the current context string formatted for log messages.
        /// </summary>
        public static string CurrentContext
        {
            get
            {
                var stack = Local.Value;
                if (stack == null || stack.Count == 0) return "";
                var contexts = new List<string>(stack);
                contexts.Reverse();
                return $"[{string.Join(" > ", contexts)}] ";
            }
        }

        private bool _disposed;

        /// <summary>Creates a new log context scope.</summary>
        public LogContext(string context)
        {
            Current.Push(context ?? string.Empty);
        }

        /// <summary>Removes this context from the stack when disposed.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var stack = Local.Value;
            if (stack != null && stack.Count > 0)
                stack.Pop();
        }

        /// <summary>Clears all contexts on the current logical thread.</summary>
        public static void Clear() => Local.Value?.Clear();
    }

    /// <summary>
    /// Provides fluent assertion API with automatic logging and actions.
    /// </summary>
    public class LogAssert
    {
        private readonly bool _condition;
        private readonly string _message;

        internal LogAssert(IJSLogger logger, bool condition, string message,
            string callerFilePath, int callerLineNumber, string callerMemberName)
        {
            _condition = condition;
            _message = message;

            if (!condition && logger != null)
            {
                logger.LogAt(LogType.Error, $"ASSERTION FAILED: {message}", null,
                    callerFilePath, callerLineNumber, callerMemberName);
            }
        }

        /// <summary>Executes a callback if the assertion fails.</summary>
        public LogAssert OnFailure(Action callback)
        {
            if (!_condition)
                callback?.Invoke();
            return this;
        }

        /// <summary>Breaks into the debugger if attached and assertion fails.</summary>
        public LogAssert BreakDebugger()
        {
            if (!_condition && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
            return this;
        }

#if UNITY_EDITOR
        /// <summary>Pauses the Unity Editor if the assertion fails.</summary>
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

        [MenuItem("IJS/Logger/Enable Logs")]
        private static void EnableUseLogs()
        {
            useLogs = true;
            OnUseLogsChanged();
        }

        [MenuItem("IJS/Logger/Disable Logs")]
        private static void DisableUseLogs()
        {
            useLogs = false;
            OnUseLogsChanged();
        }

        private static void OnUseLogsChanged() => UpdateScriptingDefineSymbols(UseLogs, useLogs);

        private static void UpdateScriptingDefineSymbols(string val, bool add)
        {
            var platform = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var definesString = PlayerSettings.GetScriptingDefineSymbols(platform);
            var allDefines = definesString.Split(';').ToList();
            if (add)
            {
                if (!allDefines.Contains(val)) allDefines.Add(val);
            }
            else
            {
                if (allDefines.Contains(val)) allDefines.Remove(val);
            }
            PlayerSettings.SetScriptingDefineSymbols(platform, string.Join(";", allDefines.ToArray()));
        }
#endif

        // -------- Global handler / sink installation --------

        private static IJSLogHandler _globalHandler;

        /// <summary>
        /// The currently installed global <see cref="IJSLogHandler"/>, or <c>null</c>
        /// if <see cref="InstallGlobalHandler"/> has not been called.
        /// </summary>
        public static IJSLogHandler GlobalHandler => _globalHandler;

        /// <summary>
        /// Installs a global <see cref="IJSLogHandler"/> on <see cref="Debug.unityLogger"/> so that
        /// every Unity log (including raw <c>Debug.Log</c> and third-party logs) is fanned out to all
        /// sinks added via <see cref="AddSink"/>. Idempotent: subsequent calls return the existing handler.
        /// </summary>
        public static IJSLogHandler InstallGlobalHandler()
        {
            if (_globalHandler != null) return _globalHandler;

            var original = Debug.unityLogger.logHandler;
            // Avoid re-wrapping if somehow we ended up with our own handler already in place.
            if (original is IJSLogHandler existing)
            {
                _globalHandler = existing;
                return _globalHandler;
            }

            _globalHandler = new IJSLogHandler(original);
            Debug.unityLogger.logHandler = _globalHandler;
            return _globalHandler;
        }

        /// <summary>
        /// Restores the original Unity log handler if <see cref="InstallGlobalHandler"/> was called.
        /// </summary>
        public static void UninstallGlobalHandler()
        {
            if (_globalHandler == null) return;
            if (Debug.unityLogger.logHandler == _globalHandler)
                Debug.unityLogger.logHandler = _globalHandler.OriginalHandler;
            _globalHandler = null;
        }

        /// <summary>
        /// Adds an additional <see cref="ILogSink"/>. Installs the global handler if not already installed.
        /// </summary>
        public static void AddSink(ILogSink sink)
        {
            if (sink == null) return;
            InstallGlobalHandler().AddSink(sink);
        }

        /// <summary>Removes a previously added sink. Returns true if it was registered.</summary>
        public static bool RemoveSink(ILogSink sink) => _globalHandler != null && _globalHandler.RemoveSink(sink);

        /// <summary>Flushes the global handler and all its sinks.</summary>
        public static void FlushAllSinks() => _globalHandler?.FlushAll();

        // -------- Instance state --------

        private Color _logColor;
        private readonly bool _isNoOpLogger;
        private bool _logsEnabled;
        private string _logPrefix;
        private LogChannel _channel;
        private string _channelId;   // when non-null, takes precedence over _channel

        /// <summary>If true, the editor formatter highlights numeric tokens in red. Defaults to false (zero-alloc).</summary>
        public bool HighlightNumbers { get; set; }

        private IJSLogger(bool logsEnabled)
        {
            _logColor = Color.white;
            _logPrefix = string.Empty;
            _logsEnabled = logsEnabled;
            _channel = LogChannel.Default;
            _channelId = null;
            _isNoOpLogger = !logsEnabled;
        }

        /// <summary>
        /// Creates a logger instance only when <paramref name="logsEnabled"/> is true and <c>USE_LOGS</c> is defined.
        /// Returns a shared no-op logger when either condition is not met.
        /// </summary>
        public static IJSLogger Create(string prefix = "", Color? color = null, bool logsEnabled = true,
            LogChannel channel = LogChannel.Default)
        {
            if (!ShouldCreateLogger(logsEnabled))
                return DisabledLogger;

            return new IJSLogger(prefix, color, logsEnabled, channel);
        }

        /// <summary>
        /// Creates a logger instance only when <paramref name="logsEnabled"/> is true and <c>USE_LOGS</c> is defined.
        /// Returns a shared no-op logger when either condition is not met.
        /// String-keyed overload: <paramref name="channelId"/> is used as the Unity console tag and
        /// for filter lookup against custom channels registered with <see cref="IJSLoggerSettings"/>.
        /// </summary>
        public static IJSLogger Create(string prefix, Color? color, bool logsEnabled, string channelId)
        {
            if (!ShouldCreateLogger(logsEnabled))
                return DisabledLogger;

            return new IJSLogger(prefix, color, logsEnabled, channelId);
        }

        /// <summary>
        /// Convenience overload that derives prefix/color/enabled state from the
        /// <paramref name="asset"/>'s defaults.
        /// </summary>
        public static IJSLogger Create(LogChannelAsset asset, string prefix = null, bool? logsEnabled = null)
        {
            if (asset == null) return Create();
            var enabled = logsEnabled ?? asset.DefaultEnabled;
            return Create(prefix ?? asset.Id, asset.Color, enabled, asset.Id);
        }

        private static bool ShouldCreateLogger(bool logsEnabled)
        {
            if (!logsEnabled) return false;
#if USE_LOGS
            return true;
#else
            return false;
#endif
        }

        public IJSLogger(string prefix = "", Color? color = null, bool logsEnabled = true,
            LogChannel channel = LogChannel.Default)
        {
            _logColor = color ?? Color.white;
            _logPrefix = prefix ?? string.Empty;
            _logsEnabled = logsEnabled;
            _channel = channel;
            _channelId = null;
            _isNoOpLogger = false;
        }

        /// <summary>String-keyed constructor; see <see cref="Create(string,Color?,bool,string)"/>.</summary>
        public IJSLogger(string prefix, Color? color, bool logsEnabled, string channelId)
        {
            _logColor = color ?? Color.white;
            _logPrefix = prefix ?? string.Empty;
            _logsEnabled = logsEnabled;
            _channel = LogChannel.Default;
            _channelId = string.IsNullOrEmpty(channelId) ? null : channelId;
            _isNoOpLogger = false;
        }

        /// <summary>
        /// Enables logging for this instance. Returns <c>false</c> only for the shared no-op logger.
        /// </summary>
        public bool EnableLogs()
        {
            if (_isNoOpLogger) return false;
            _logsEnabled = true;
            return true;
        }

        /// <summary>
        /// Disables logging for this instance.
        /// Returns <c>true</c> when the state changes and <c>false</c> when the logger is already disabled
        /// or is the shared no-op logger.
        /// </summary>
        public bool DisableLogs()
        {
            if (_isNoOpLogger) return false;
            if (!_logsEnabled) return false;
            _logsEnabled = false;
            return true;
        }

        [Obsolete("Use EnableLogs() or DisableLogs() instead.")]
        public void ToggleLogs(bool enable)
        {
            if (enable) EnableLogs();
            else DisableLogs();
        }

        public void ModifyPrefix(string prefix)
        {
            if (_isNoOpLogger) return;
            _logPrefix = prefix ?? string.Empty;
        }

        public void ModifyColor(Color color)
        {
            if (_isNoOpLogger) return;
            _logColor = color;
        }

        // -------- Assertions / Validation --------

        /// <summary>Asserts a condition and logs an error if it fails.</summary>
        public LogAssert Assert(bool condition, string message,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            return new LogAssert(this, condition, message, callerFilePath, callerLineNumber, callerMemberName);
        }

        public void ValidateNotNull(object obj, string paramName,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            Assert(obj != null, $"{paramName} cannot be null", callerFilePath, callerLineNumber, callerMemberName);
        }

        public void ValidateRange(float value, float min, float max, string paramName,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            Assert(value >= min && value <= max,
                $"{paramName} must be between {min} and {max}, but was {value}",
                callerFilePath, callerLineNumber, callerMemberName);
        }

        // -------- Conditional / Throttled logging --------

        /// <summary>Logs a message only if the condition is true.</summary>
        [Conditional("USE_LOGS")]
        public void LogIf(bool condition, string message, LogType logType = LogType.Log, GameObject go = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (!_logsEnabled || !condition) return;
            LogAt(logType, message, go, callerFilePath, callerLineNumber, callerMemberName);
        }

        /// <summary>
        /// Logs a message only if the condition function returns true. Uses lazy evaluation so
        /// neither the condition nor the message builder runs when logging is disabled.
        /// </summary>
        [Conditional("USE_LOGS")]
        public void LogIf(Func<bool> condition, Func<string> messageBuilder, LogType logType = LogType.Log,
            GameObject go = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (!_logsEnabled || condition == null || messageBuilder == null) return;
            if (!condition()) return;
            LogAt(logType, messageBuilder(), go, callerFilePath, callerLineNumber, callerMemberName);
        }

        /// <summary>Logs a message with rate limiting to prevent spam.</summary>
        [Conditional("USE_LOGS")]
        public void LogThrottled(string message, float minIntervalSeconds, LogType logType = LogType.Log,
            GameObject go = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (!_logsEnabled) return;
            // Use file+line as the throttle key so the same log statement throttles regardless of
            // dynamic message content. Falls back to message content if caller info is missing.
            var key = string.IsNullOrEmpty(callerFilePath)
                ? $"{GetHashCode()}_{message}"
                : $"{callerFilePath}:{callerLineNumber}";

            if (!LogRateLimiter.ShouldLog(key, minIntervalSeconds)) return;

            var suppressedCount = LogRateLimiter.GetSuppressedCount(key);
            var finalMessage = suppressedCount > 0 ? $"{message} (suppressed {suppressedCount}x)" : message;
            LogAt(logType, finalMessage, go, callerFilePath, callerLineNumber, callerMemberName);
        }

        // -------- Core logging entry points --------

        [Conditional("USE_LOGS")]
        public void PrintLog(string message, LogType logType = LogType.Log, GameObject go = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (!_logsEnabled) return;
            LogAt(logType, message, go, callerFilePath, callerLineNumber, callerMemberName);
        }

        /// <summary>Logs a real exception, preserving the type and stack trace through the pipeline.</summary>
        [Conditional("USE_LOGS")]
        public void PrintException(Exception exception, GameObject go = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (!_logsEnabled || exception == null) return;
            if (!IsAllowed(LogType.Exception)) return;
            if (!string.IsNullOrEmpty(_channelId))
            {
                if (!IJSLoggerSettings.IsChannelEnabled(_channelId)) return;
            }
            else if (!IJSLoggerSettings.IsChannelEnabled(_channel))
            {
                return;
            }

            // Route through Debug.unityLogger so that, when the global handler is installed,
            // sinks receive the exception too. The global handler's IJSLogHandler.LogException
            // takes care of console + additional sinks via the captured original handler.
            Debug.unityLogger.LogException(exception, go);
        }

        /// <summary>
        /// Internal entry point that all conditional/public methods funnel through, so caller-info
        /// from the original call site is preserved.
        /// </summary>
        internal void LogAt(LogType logType, string message, GameObject go,
            string callerFilePath, int callerLineNumber, string callerMemberName)
        {
            if (!_logsEnabled) return;
            if (!IsChannelAllowed(_channel, _channelId, logType)) return;

            // Add context if any.
            var contextPrefix = LogContext.CurrentContext;
            if (!string.IsNullOrEmpty(contextPrefix))
                message = contextPrefix + message;

            // Add per-logger prefix.
            if (!string.IsNullOrEmpty(_logPrefix))
                message = $"{_logPrefix}:: {message}";

            var formatted = FormatMessage(message, _logColor, HighlightNumbers, callerFilePath, callerLineNumber);

            EmitToPipeline(logType, _channel, ResolveTag(), formatted, go,
                callerFilePath, callerLineNumber, callerMemberName);
        }

        private string ResolveTag()
        {
            if (!string.IsNullOrEmpty(_channelId)) return _channelId;
            return _channel == LogChannel.Default ? null : _channel.ToString();
        }

        private static bool IsAllowed(LogType logType)
        {
            var settings = IJSLoggerSettings.Instance;
            return settings == null || settings.IsLogTypeAllowed(logType);
        }

        private static bool IsChannelAllowed(LogChannel channel, LogType logType)
        {
            if (!IsAllowed(logType)) return false;
            if (!IJSLoggerSettings.IsChannelEnabled(channel)) return false;
            var settings = IJSLoggerSettings.Instance;
            return settings == null || settings.IsChannelLogTypeAllowed(channel, logType);
        }

        // Combined check that prefers the string id when present, else falls back to the enum channel.
        private static bool IsChannelAllowed(LogChannel channel, string channelId, LogType logType)
        {
            if (!IsAllowed(logType)) return false;
            if (!string.IsNullOrEmpty(channelId))
            {
                if (!IJSLoggerSettings.IsChannelEnabled(channelId)) return false;
                var s = IJSLoggerSettings.Instance;
                return s == null || s.IsChannelLogTypeAllowed(channelId, logType);
            }
            if (!IJSLoggerSettings.IsChannelEnabled(channel)) return false;
            var settings = IJSLoggerSettings.Instance;
            return settings == null || settings.IsChannelLogTypeAllowed(channel, logType);
        }

        // -------- Static API (kept for backward compat, now routed through the pipeline) --------

        /// <summary>
        /// Logs a message via the same pipeline as instance loggers (channel filter, sinks, caller-info link).
        /// Uses <see cref="LogChannel.Default"/>.
        /// </summary>
        [Conditional("USE_LOGS")]
        public static void Log(
            string message,
            LogType type = LogType.Log,
            GameObject go = null,
            Color? color = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            if (!IsChannelAllowed(LogChannel.Default, type)) return;

            var contextPrefix = LogContext.CurrentContext;
            if (!string.IsNullOrEmpty(contextPrefix))
                message = contextPrefix + message;

            var formatted = FormatMessage(message, color ?? Color.white, false, callerFilePath, callerLineNumber);
            EmitToPipeline(type, LogChannel.Default, null, formatted, go,
                callerFilePath, callerLineNumber, callerMemberName);
        }

        // -------- Formatting & emission --------

        private static string FormatMessage(string message, Color color, bool highlightNumbers,
            string callerFilePath, int callerLineNumber)
        {
#if UNITY_EDITOR
            var body = highlightNumbers ? HighlightNumericTokens(message) : message;
            var hex = $"{(byte)(color.r * 255f):X2}{(byte)(color.g * 255f):X2}{(byte)(color.b * 255f):X2}";
            var formatted = $"<size=11><i><b><color=#{hex}>{body}</color></b></i></size>";
#else
            var formatted = message;
#endif
            // Append a Unity-recognized link line so the console double-click jumps back to
            // the original call site (instead of into IJSLogger.cs).
            if (!string.IsNullOrEmpty(callerFilePath))
            {
                var rel = ToRelativeAssetPath(callerFilePath);
                formatted += $"\n(at {rel}:{callerLineNumber})";
            }
            return formatted;
        }

        private static string ToRelativeAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;
            var normalized = fullPath.Replace('\\', '/');

            // Try to make the path relative to the project root (parent of "Assets").
            string projectRoot = null;
            try
            {
                if (!string.IsNullOrEmpty(Application.dataPath))
                {
                    var dataPath = Application.dataPath.Replace('\\', '/');
                    if (dataPath.EndsWith("/Assets", StringComparison.Ordinal))
                        projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);
                }
            }
            catch
            {
                // Application.dataPath is main-thread only in some Unity versions.
            }

            if (!string.IsNullOrEmpty(projectRoot) &&
                normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(projectRoot.Length);
            }

            // Fallback: trim everything up to and including the last "/Assets/" or "/Packages/" segment.
            var assetsIdx = normalized.LastIndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx >= 0) return normalized.Substring(assetsIdx + 1);
            var pkgIdx = normalized.LastIndexOf("/Packages/", StringComparison.OrdinalIgnoreCase);
            if (pkgIdx >= 0) return normalized.Substring(pkgIdx + 1);

            return normalized;
        }

#if UNITY_EDITOR
        private static string HighlightNumericTokens(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;

            // Single-pass scan: wrap maximal runs of digits (with optional surrounding sign/dot)
            // with the highlight tags. Avoids splitting/joining the entire string per word.
            var sb = new System.Text.StringBuilder(message.Length + 32);
            var i = 0;
            while (i < message.Length)
            {
                var c = message[i];
                if (char.IsDigit(c))
                {
                    var start = i;
                    while (i < message.Length && (char.IsDigit(message[i]) || message[i] == '.'))
                        i++;
                    // Only highlight if the run is not adjacent to letters (avoid mangling identifiers like a1b2).
                    var prevOk = start == 0 || !char.IsLetter(message[start - 1]);
                    var nextOk = i == message.Length || !char.IsLetter(message[i]);
                    if (prevOk && nextOk)
                    {
                        sb.Append("<size=13><color=#FF214C>");
                        sb.Append(message, start, i - start);
                        sb.Append("</color></size>");
                    }
                    else
                    {
                        sb.Append(message, start, i - start);
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }
#endif

        private static void EmitToPipeline(LogType logType, LogChannel channel, string tag,
            string formattedMessage, Object context,
            string callerFilePath, int callerLineNumber, string callerMemberName)
        {
            // If a global handler is installed it will receive this log via Debug.unityLogger
            // and dispatch to all sinks itself. Use the tag overload so the channel name appears
            // as Unity's [Tag] in the console and benefits from Unity's filtering UI.
            var unityLogger = Debug.unityLogger;

            if (logType == LogType.Exception)
            {
                unityLogger.LogException(new Exception(formattedMessage), context);
                return;
            }

            // Cast disambiguates the overload: ILogger has both Log(LogType, object, ...) and
            // Log(LogType, object, object, ...). The (object) cast keeps the tag overload selected
            // even if `tag` is null in the future or when callers pass a string literal.
            if (!string.IsNullOrEmpty(tag))
                unityLogger.Log(logType, (object)tag, formattedMessage, context);
            else
                unityLogger.Log(logType, (object)formattedMessage, context);

            // If no global handler is installed but additional sinks were added, we still want
            // them to receive entries originating from instance loggers. Currently AddSink always
            // installs the global handler, so this branch is intentionally left empty.
        }
    }
}
