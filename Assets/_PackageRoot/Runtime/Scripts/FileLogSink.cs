using System;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.ijs.logger
{
    /// <summary>
    /// Writes log entries to a file with size-based rotation. Thread-safe.
    /// </summary>
    /// <remarks>
    /// On reaching <see cref="MaxFileSizeBytes"/>, the current file is renamed
    /// to <c>{name}.1{ext}</c>, the previous <c>.1</c> becomes <c>.2</c>, and so
    /// on up to <see cref="MaxRolledFiles"/>. The oldest file is discarded.
    /// </remarks>
    public class FileLogSink : ILogSink, IDisposable
    {
        private readonly string _filePath;
        private readonly long _maxFileSizeBytes;
        private readonly int _maxRolledFiles;
        private readonly object _gate = new object();
        private StreamWriter _writer;
        private long _currentSize;
        private bool _disposed;

        /// <summary>Maximum size of the active log file before rotation, in bytes.</summary>
        public long MaxFileSizeBytes => _maxFileSizeBytes;

        /// <summary>Maximum number of rolled-over files kept on disk.</summary>
        public int MaxRolledFiles => _maxRolledFiles;

        /// <summary>Absolute path of the active log file.</summary>
        public string FilePath => _filePath;

        /// <summary>
        /// Creates a new file sink.
        /// </summary>
        /// <param name="filePath">Absolute path to the log file. The containing directory is created if missing.</param>
        /// <param name="maxFileSizeBytes">Active file size at which rotation triggers. Defaults to 5 MiB.</param>
        /// <param name="maxRolledFiles">Number of rolled files to retain. Defaults to 5.</param>
        public FileLogSink(string filePath, long maxFileSizeBytes = 5L * 1024 * 1024, int maxRolledFiles = 5)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath must be non-empty", nameof(filePath));
            if (maxFileSizeBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes));
            if (maxRolledFiles < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRolledFiles));

            _filePath = filePath;
            _maxFileSizeBytes = maxFileSizeBytes;
            _maxRolledFiles = maxRolledFiles;

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            OpenWriter();
        }

        private void OpenWriter()
        {
            var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            _currentSize = stream.Length;
        }

        public void Write(in LogEntry entry)
        {
            if (_disposed) return;

            // Build the line outside the lock to keep the critical section small.
            var sb = new StringBuilder(128);
            sb.Append(entry.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            sb.Append(' ').Append(LevelLabel(entry.LogType));
            if (!string.IsNullOrEmpty(entry.Tag))
                sb.Append(" [").Append(entry.Tag).Append(']');
            else if (entry.Channel != LogChannel.Default)
                sb.Append(" [").Append(entry.Channel).Append(']');

            sb.Append(' ').Append(entry.Message);

            if (!string.IsNullOrEmpty(entry.CallerFilePath))
            {
                sb.Append(" (at ");
                sb.Append(entry.CallerFilePath);
                sb.Append(':').Append(entry.CallerLineNumber);
                sb.Append(')');
            }

            var line = sb.ToString();
            // Use byte count, not character count, so rotation triggers correctly
            // for messages containing multi-byte UTF-8 characters.
            var bytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            lock (_gate)
            {
                if (_writer == null) return;
                _writer.WriteLine(line);
                _currentSize += bytes;
                if (_currentSize >= _maxFileSizeBytes)
                    Rotate();
            }
        }

        public void WriteException(Exception exception, LogChannel channel, string tag, Object context)
        {
            if (exception == null || _disposed) return;
            var entry = new LogEntry(LogType.Exception, channel, tag,
                exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace,
                context, "", 0, "");
            Write(entry);
        }

        public void Flush()
        {
            lock (_gate)
            {
                _writer?.Flush();
            }
        }

        private void Rotate()
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;

                if (_maxRolledFiles == 0)
                {
                    if (File.Exists(_filePath)) File.Delete(_filePath);
                }
                else
                {
                    var ext = Path.GetExtension(_filePath);
                    var baseName = _filePath.Substring(0, _filePath.Length - ext.Length);

                    var oldest = baseName + "." + _maxRolledFiles + ext;
                    if (File.Exists(oldest)) File.Delete(oldest);

                    for (var i = _maxRolledFiles - 1; i >= 1; i--)
                    {
                        var src = baseName + "." + i + ext;
                        var dst = baseName + "." + (i + 1) + ext;
                        if (File.Exists(src)) File.Move(src, dst);
                    }

                    if (File.Exists(_filePath))
                        File.Move(_filePath, baseName + ".1" + ext);
                }
            }
            catch (Exception ex)
            {
                // Avoid recursive failure: surface the rotation problem via the original handler only.
                Debug.unityLogger.LogException(ex, null);
            }
            finally
            {
                OpenWriter();
            }
        }

        private static string LevelLabel(LogType t)
        {
            switch (t)
            {
                case LogType.Error: return "ERROR";
                case LogType.Assert: return "ASSERT";
                case LogType.Warning: return "WARN ";
                case LogType.Log: return "INFO ";
                case LogType.Exception: return "EXCPT";
                default: return t.ToString();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                try { _writer?.Flush(); _writer?.Dispose(); } catch { /* swallow */ }
                _writer = null;
            }
        }
    }
}
