using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.ijs.logger
{
    /// <summary>
    /// Default sink that writes to the Unity console via the original <see cref="ILogHandler"/>.
    /// Captures Unity's original log handler at construction so that installing a custom
    /// <see cref="IJSLogHandler"/> globally does not cause infinite recursion.
    /// </summary>
    public class UnityConsoleSink : ILogSink
    {
        private readonly ILogHandler _handler;

        /// <summary>
        /// Creates a sink that writes through the supplied handler. If <paramref name="handler"/>
        /// is null, the current <see cref="Debug.unityLogger"/>'s handler is captured.
        /// </summary>
        public UnityConsoleSink(ILogHandler handler = null)
        {
            _handler = handler ?? Debug.unityLogger.logHandler;
        }

        public void Write(in LogEntry entry)
        {
            // Use LogFormat with a literal "{0}" so curly braces in the message itself
            // do not trip the underlying string.Format call.
            _handler.LogFormat(entry.LogType, entry.Context, "{0}", entry.Message);
        }

        public void WriteException(Exception exception, LogChannel channel, string tag, Object context)
        {
            if (exception == null) return;
            _handler.LogException(exception, context);
        }

        public void Flush()
        {
            // Unity console flushes itself.
        }
    }
}
