using UnityEngine;

namespace com.ijs.logger
{
    /// <summary>
    /// User-defined log channel, declared as a <see cref="ScriptableObject"/> asset.
    /// Lets projects add or update channels without modifying the built-in <see cref="LogChannel"/>
    /// enum: create one of these via <c>Assets -> Create -> IJS -> Log Channel</c>,
    /// then either reference it directly or pass <see cref="Id"/> to the string-channel overloads
    /// of <see cref="IJSLogger.Create(string,Color?,bool,string)"/>.
    /// </summary>
    /// <remarks>
    /// The asset's identity is its <see cref="Id"/> string (defaults to the asset name).
    /// All runtime filtering is keyed on that id, so renaming the asset will only break configs
    /// if you also clear the explicit id field.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewLogChannel", menuName = "IJS/Log Channel")]
    public class LogChannelAsset : ScriptableObject
    {
        [Tooltip("Stable identifier for this channel. Defaults to the asset name. Used as the tag in the Unity console.")]
        [SerializeField] private string id;

        [Tooltip("Display color for loggers created from this channel.")]
        [SerializeField] private Color color = Color.white;

        [Tooltip("Default enabled state when this channel is first registered with IJSLoggerSettings.")]
        [SerializeField] private bool defaultEnabled = true;

        [Tooltip("Default scope (Editor/Build/Both) when this channel is first registered with IJSLoggerSettings.")]
        [SerializeField] private ChannelScope defaultScope = ChannelScope.Both;

        [Tooltip("Default minimum log severity for this channel.")]
        [SerializeField] private LogType defaultMinLogType = LogType.Log;

        /// <summary>Stable id used for filtering. Falls back to the asset name when blank.</summary>
        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public Color Color => color;
        public bool DefaultEnabled => defaultEnabled;
        public ChannelScope DefaultScope => defaultScope;
        public LogType DefaultMinLogType => defaultMinLogType;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
                id = name;
        }
    }
}
