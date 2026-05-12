using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.ijs.logger
{
    /// <summary>
    /// Custom <see cref="ILogHandler"/> that intercepts every log going through
    /// <see cref="Debug.unityLogger"/> (including raw <c>Debug.Log</c> calls and
    /// third-party packages), applies <see cref="IJSLoggerSettings"/> filtering
    /// (severity, channel, etc.), and fans out to all registered <see cref="ILogSink"/>s.
    /// </summary>
    /// <remarks>
    /// Install via <see cref="IJSLogger.InstallGlobalHandler"/>. The original handler
    /// is preserved and used by the default <see cref="UnityConsoleSink"/> so the
    /// Unity console keeps working without infinite recursion.
    /// </remarks>
    public class IJSLogHandler : ILogHandler
    {
        private readonly ILogHandler _originalHandler;
        private readonly UnityConsoleSink _consoleSink;
        private readonly List<ILogSink> _additionalSinks = new List<ILogSink>();
        private readonly object _sinksGate = new object();

        // Re-entrancy guard per thread: prevents recursion if a sink itself calls Debug.Log.
        [ThreadStatic] private static bool _inHandler;

        public ILogHandler OriginalHandler => _originalHandler;

        public IJSLogHandler(ILogHandler originalHandler)
        {
            _originalHandler = originalHandler ?? throw new ArgumentNullException(nameof(originalHandler));
            _consoleSink = new UnityConsoleSink(_originalHandler);
        }

        /// <summary>Adds an extra sink. Console output is always emitted via the captured original handler.</summary>
        public void AddSink(ILogSink sink)
        {
            if (sink == null) return;
            lock (_sinksGate) _additionalSinks.Add(sink);
        }

        /// <summary>Removes a previously added sink. Returns true if the sink was present.</summary>
        public bool RemoveSink(ILogSink sink)
        {
            if (sink == null) return false;
            lock (_sinksGate) return _additionalSinks.Remove(sink);
        }

        /// <summary>Flushes all sinks.</summary>
        public void FlushAll()
        {
            _consoleSink.Flush();
            lock (_sinksGate)
            {
                for (var i = 0; i < _additionalSinks.Count; i++)
                {
                    try { _additionalSinks[i].Flush(); }
                    catch (Exception ex) { _originalHandler.LogException(ex, null); }
                }
            }
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (_inHandler)
            {
                // Safety net: never recurse, fall back to the original handler.
                _originalHandler.LogFormat(logType, context, format, args);
                return;
            }

            var settings = IJSLoggerSettings.Instance;
            if (settings != null && !settings.IsLogTypeAllowed(logType))
                return;

            _inHandler = true;
            try
            {
                string message;
                try { message = args == null || args.Length == 0 ? format : string.Format(format, args); }
                catch { message = format; }

                var entry = new LogEntry(logType, LogChannel.Default, null, message, context, "", 0, "");
                Dispatch(in entry);
            }
            finally
            {
                _inHandler = false;
            }
        }

        public void LogException(Exception exception, Object context)
        {
            if (_inHandler)
            {
                _originalHandler.LogException(exception, context);
                return;
            }

            var settings = IJSLoggerSettings.Instance;
            if (settings != null && !settings.IsLogTypeAllowed(LogType.Exception))
                return;

            _inHandler = true;
            try
            {
                _consoleSink.WriteException(exception, LogChannel.Default, null, context);
                lock (_sinksGate)
                {
                    for (var i = 0; i < _additionalSinks.Count; i++)
                    {
                        try { _additionalSinks[i].WriteException(exception, LogChannel.Default, null, context); }
                        catch (Exception ex) { _originalHandler.LogException(ex, null); }
                    }
                }
            }
            finally
            {
                _inHandler = false;
            }
        }

        internal void Dispatch(in LogEntry entry)
        {
            // Always goes to the Unity console first.
            _consoleSink.Write(in entry);

            lock (_sinksGate)
            {
                for (var i = 0; i < _additionalSinks.Count; i++)
                {
                    try { _additionalSinks[i].Write(in entry); }
                    catch (Exception ex) { _originalHandler.LogException(ex, null); }
                }
            }
        }
    }
}
