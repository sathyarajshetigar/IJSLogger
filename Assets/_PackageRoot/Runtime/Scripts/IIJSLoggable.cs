using System;

namespace com.ijs.logger
{
    /// <summary>
    /// Marker interface that gives any class instance a logger via the
    /// <see cref="IJSLoggableExtensions"/> extension methods, without requiring a
    /// per-class <c>IJSLogger</c> field. Combine with <see cref="LogConfigAttribute"/>
    /// to declare prefix/color/channel on the type itself.
    /// </summary>
    /// <example>
    /// <code>
    /// [LogConfig(prefix = "Audio", channel = LogChannel.Audio, colorHex = "#FFD000")]
    /// public class AudioMixer : MonoBehaviour, IIJSLoggable
    /// {
    ///     void Play() => this.Log("playing");   // no fields, no IJSLogger.Create call
    /// }
    /// </code>
    /// </example>
    public interface IIJSLoggable
    {
    }

    /// <summary>
    /// Optional declarative configuration for a type implementing <see cref="IIJSLoggable"/>.
    /// Read once per type by <see cref="IJSLoggerRegistry"/> and cached.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class LogConfigAttribute : Attribute
    {
        /// <summary>Prefix prepended to each log message. Defaults to the type name when null.</summary>
        public string prefix;

        /// <summary>
        /// Hex color for editor formatting (e.g. <c>"#FFD000"</c> or <c>"#FFD000FF"</c>).
        /// Falls back to white when blank or unparseable.
        /// </summary>
        public string colorHex;

        /// <summary>Built-in enum channel. Ignored when <see cref="channelId"/> is set.</summary>
        public LogChannel channel = LogChannel.Default;

        /// <summary>Custom string-keyed channel id (e.g. matches a <see cref="LogChannelAsset"/>).</summary>
        public string channelId;

        /// <summary>Whether the per-type logger starts enabled. Defaults to true.</summary>
        public bool logsEnabled = true;

        /// <summary>Mirrors <see cref="IJSLogger.HighlightNumbers"/>.</summary>
        public bool highlightNumbers;
    }
}
