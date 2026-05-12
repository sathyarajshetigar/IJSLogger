using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.ijs.logger
{
    /// <summary>
    /// Configuration data for a single log channel.
    /// </summary>
    [Serializable]
    public class ChannelConfig
    {
        public LogChannel channel;
        public ChannelScope scope = ChannelScope.Both;
        public bool enabled = true;
        [Tooltip("Minimum log severity allowed for this channel. Logs below this level are suppressed even if the channel is enabled.")]
        public LogType minLogType = LogType.Log;

        public ChannelConfig(LogChannel channel, ChannelScope scope = ChannelScope.Both, bool enabled = true,
            LogType minLogType = LogType.Log)
        {
            this.channel = channel;
            this.scope = scope;
            this.enabled = enabled;
            this.minLogType = minLogType;
        }
    }

    /// <summary>
    /// Settings for IJSLogger system. Manages channel configurations and filtering.
    /// </summary>
    [CreateAssetMenu(fileName = "IJSLoggerSettings", menuName = "IJS/Logger Settings")]
    public class IJSLoggerSettings : ScriptableObject
    {
        [Header("Channel Configuration")]
        [Tooltip("Configuration for each log channel")]
        [SerializeField] private List<ChannelConfig> channelConfigs = new List<ChannelConfig>();

        [Header("Rate Limiting")]
        [Tooltip("Enable global rate limiting")]
        [SerializeField] private bool enableRateLimiting = true;

        [Tooltip("Default rate limit in seconds")]
        [SerializeField] private float defaultRateLimitSeconds = 0.1f;

        [Header("Global Filtering")]
        [Tooltip("Master switch. When false, all logs from this system are suppressed and Debug.unityLogger.logEnabled is set to false on Apply.")]
        [SerializeField] private bool logsEnabled = true;

        [Tooltip("Minimum severity for any log to be emitted. Maps to Debug.unityLogger.filterLogType on Apply.")]
        [SerializeField] private LogType globalMinLogType = LogType.Log;

        private static IJSLoggerSettings _instance;
        private static bool _instanceSearched;

        /// <summary>
        /// Gets the singleton instance of IJSLoggerSettings.
        /// </summary>
        public static IJSLoggerSettings Instance
        {
            get
            {
                if (_instance == null && !_instanceSearched)
                {
                    _instanceSearched = true;
                    _instance = Resources.Load<IJSLoggerSettings>("IJSLoggerSettings");

                    // Initialize default settings if not found
                    if (_instance == null)
                    {
                        Debug.LogWarning("[IJSLogger] No IJSLoggerSettings found in Resources folder. Using default settings. " +
                                         "Create one via Assets -> Create -> IJS -> Logger Settings and place it in a Resources folder.");
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Checks if a channel is enabled based on current scope (editor vs build) and configuration.
        /// </summary>
        public static bool IsChannelEnabled(LogChannel channel)
        {
            // Default channel is always enabled
            if (channel == LogChannel.Default)
                return true;

            var instance = Instance;
            if (instance == null)
                return true; // If no settings, allow all channels

            var config = instance.FindConfig(channel);
            if (config == null)
                return true; // If channel not configured, default to enabled

            if (!config.enabled)
                return false;

            // Check scope
#if UNITY_EDITOR
            return config.scope == ChannelScope.EditorOnly || config.scope == ChannelScope.Both;
#else
            return config.scope == ChannelScope.BuildOnly || config.scope == ChannelScope.Both;
#endif
        }

        /// <summary>
        /// Gets the configuration for a specific channel.
        /// </summary>
        public ChannelConfig GetChannelConfig(LogChannel channel)
        {
            return FindConfig(channel);
        }

        // Hot-path lookup — avoids LINQ allocations on every log call. The list is tiny
        // (at most one entry per LogChannel) so a manual scan is faster than building/maintaining
        // a separate dictionary that would also need invalidation across serialization changes.
        private ChannelConfig FindConfig(LogChannel channel)
        {
            var list = channelConfigs;
            if (list == null) return null;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].channel == channel)
                    return list[i];
            }
            return null;
        }

        /// <summary>
        /// Sets whether a channel is enabled.
        /// </summary>
        public void SetChannelEnabled(LogChannel channel, bool enabled)
        {
            var config = channelConfigs.FirstOrDefault(c => c.channel == channel);
            if (config != null)
            {
                config.enabled = enabled;
            }
            else
            {
                channelConfigs.Add(new ChannelConfig(channel, ChannelScope.Both, enabled));
            }
        }

        /// <summary>
        /// Sets the scope for a channel.
        /// </summary>
        public void SetChannelScope(LogChannel channel, ChannelScope scope)
        {
            var config = channelConfigs.FirstOrDefault(c => c.channel == channel);
            if (config != null)
            {
                config.scope = scope;
            }
            else
            {
                channelConfigs.Add(new ChannelConfig(channel, scope, true));
            }
        }

        /// <summary>
        /// Initializes default channel configurations.
        /// </summary>
        public void InitializeDefaults()
        {
            channelConfigs.Clear();

            // Add all channels with default settings
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                if (channel == LogChannel.Default)
                    continue;

                // Performance logs only in editor by default
                var scope = channel == LogChannel.Performance ? ChannelScope.EditorOnly : ChannelScope.Both;
                channelConfigs.Add(new ChannelConfig(channel, scope, true));
            }
        }

        private void OnValidate()
        {
            // Ensure we have at least the basic channels
            if (channelConfigs.Count == 0)
            {
                InitializeDefaults();
            }
        }

        public bool EnableRateLimiting => enableRateLimiting;
        public float DefaultRateLimitSeconds => defaultRateLimitSeconds;

        /// <summary>Master logging switch.</summary>
        public bool LogsEnabled
        {
            get => logsEnabled;
            set { logsEnabled = value; ApplyToUnityLogger(); }
        }

        /// <summary>Minimum severity for any log to be emitted.</summary>
        public LogType GlobalMinLogType
        {
            get => globalMinLogType;
            set { globalMinLogType = value; ApplyToUnityLogger(); }
        }

        /// <summary>
        /// Returns true if logs of <paramref name="logType"/> should be allowed by the global filter.
        /// </summary>
        public bool IsLogTypeAllowed(LogType logType)
        {
            if (!logsEnabled) return false;
            return IsAtLeast(logType, globalMinLogType);
        }

        /// <summary>
        /// Static convenience wrapper used by <see cref="IJSLogger"/>: returns true if no settings instance exists.
        /// </summary>
        public static bool IsLogTypeAllowedGlobal(LogType logType)
        {
            var i = Instance;
            return i == null || i.IsLogTypeAllowed(logType);
        }

        /// <summary>
        /// Returns true if a log of <paramref name="logType"/> on <paramref name="channel"/>
        /// satisfies the channel's per-severity filter.
        /// </summary>
        public bool IsChannelLogTypeAllowed(LogChannel channel, LogType logType)
        {
            if (channel == LogChannel.Default) return true;
            var config = FindConfig(channel);
            if (config == null) return true;
            return IsAtLeast(logType, config.minLogType);
        }

        /// <summary>Applies <see cref="LogsEnabled"/> and <see cref="GlobalMinLogType"/> to <c>Debug.unityLogger</c>.</summary>
        public void ApplyToUnityLogger()
        {
            try
            {
                Debug.unityLogger.logEnabled = logsEnabled;
                Debug.unityLogger.filterLogType = globalMinLogType;
            }
            catch
            {
                // Debug.unityLogger may not be available outside the player loop in some contexts.
            }
        }

        // LogType ordering (most-severe first): Exception > Error > Assert > Warning > Log.
        // We treat anything at-or-above the configured min as allowed.
        private static int Severity(LogType t)
        {
            switch (t)
            {
                case LogType.Log: return 0;
                case LogType.Warning: return 1;
                case LogType.Assert: return 2;
                case LogType.Error: return 3;
                case LogType.Exception: return 4;
                default: return 0;
            }
        }

        private static bool IsAtLeast(LogType actual, LogType minimum) =>
            Severity(actual) >= Severity(minimum);
    }
}
