using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.ijs.logger
{
    /// <summary>
    /// Information about a single log entry passed to an <see cref="ILogSink"/>.
    /// </summary>
    public readonly struct LogEntry
    {
        public readonly LogType LogType;
        public readonly LogChannel Channel;
        public readonly string Tag;
        public readonly string Message;
        public readonly Object Context;
        public readonly string CallerFilePath;
        public readonly int CallerLineNumber;
        public readonly string CallerMemberName;
        public readonly DateTime TimestampUtc;

        public LogEntry(LogType logType, LogChannel channel, string tag, string message,
            Object context, string callerFilePath, int callerLineNumber, string callerMemberName)
        {
            LogType = logType;
            Channel = channel;
            Tag = tag;
            Message = message;
            Context = context;
            CallerFilePath = callerFilePath;
            CallerLineNumber = callerLineNumber;
            CallerMemberName = callerMemberName;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// A pluggable log destination. Implementations may write to the Unity console,
    /// a file, the network, an in-game overlay, a crash reporter, etc.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// Writes a single log entry to this sink.
        /// </summary>
        void Write(in LogEntry entry);

        /// <summary>
        /// Writes an exception to this sink, preserving the exception object so that
        /// implementations can use <see cref="Exception.StackTrace"/> when appropriate.
        /// </summary>
        void WriteException(Exception exception, LogChannel channel, string tag, Object context);

        /// <summary>
        /// Flushes any buffered output. Called on application quit or when the user
        /// requests an explicit flush.
        /// </summary>
        void Flush();
    }
}
