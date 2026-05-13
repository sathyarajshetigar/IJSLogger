using System;
using System.Collections.Concurrent;
using System.Reflection;
using UnityEngine;

namespace com.ijs.logger
{
    /// <summary>
    /// Per-<see cref="Type"/> cache of <see cref="IJSLogger"/> instances for objects implementing
    /// <see cref="IIJSLoggable"/>. Reads <see cref="LogConfigAttribute"/> from the type to derive
    /// prefix, color and channel; falls back to <c>type.Name</c> + <see cref="LogChannel.Default"/>
    /// when the attribute is absent.
    /// </summary>
    public static class IJSLoggerRegistry
    {
        private static readonly ConcurrentDictionary<Type, IJSLogger> Cache =
            new ConcurrentDictionary<Type, IJSLogger>();

        /// <summary>Gets (or creates) the cached logger for <paramref name="type"/>.</summary>
        public static IJSLogger For(Type type)
        {
            if (type == null) return IJSLogger.Create(logsEnabled: false);
            return Cache.GetOrAdd(type, BuildLogger);
        }

        /// <summary>
        /// Replaces the cached logger for <paramref name="type"/>. Useful for tests or for
        /// per-instance overrides where the marker interface alone is not enough.
        /// </summary>
        public static void Set(Type type, IJSLogger logger)
        {
            if (type == null || logger == null) return;
            Cache[type] = logger;
        }

        /// <summary>Clears the registry. Mostly useful from tests.</summary>
        public static void Clear() => Cache.Clear();

        private static IJSLogger BuildLogger(Type type)
        {
            var cfg = type.GetCustomAttribute<LogConfigAttribute>(inherit: true);

            var prefix = cfg?.prefix ?? type.Name;
            var color = ParseColor(cfg?.colorHex);
            var enabled = cfg?.logsEnabled ?? true;

            IJSLogger logger;
            if (cfg != null && !string.IsNullOrEmpty(cfg.channelId))
                logger = IJSLogger.Create(prefix, color, enabled, cfg.channelId);
            else
                logger = IJSLogger.Create(prefix, color, enabled, cfg?.channel ?? LogChannel.Default);

            if (cfg != null && cfg.highlightNumbers)
                logger.HighlightNumbers = true;
            return logger;
        }

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
