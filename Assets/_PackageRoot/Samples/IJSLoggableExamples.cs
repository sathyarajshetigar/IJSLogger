using System;
using UnityEngine;
using com.ijs.logger;

/// <summary>
/// Demonstrates the IIJSLoggable marker pattern: no per-class IJSLogger field is required.
/// Drop <see cref="IIJSLoggable"/> on the class and (optionally) annotate with
/// <see cref="LogConfigAttribute"/> to declare prefix/color/channel in one place.
/// </summary>
[LogConfig(prefix = "Audio", channel = LogChannel.Audio, colorHex = "#FFD000")]
public class IJSLoggableAudioExample : MonoBehaviour, IIJSLoggable
{
    private void Start()
    {
        // No IJSLogger field, no IJSLogger.Create call: the extension methods resolve a
        // cached per-Type logger via IJSLoggerRegistry. Caller info is preserved so the
        // Unity console link still jumps back to this line.
        this.Log("Audio system online");
        this.LogWarning("Mixer running hot");

        // Lazy / throttled / assert helpers all work the same way.
        this.LogIf(() => Time.time > 0f, () => $"t = {Time.time:F2}");
        this.LogThrottled("Polling audio device", 1.0f);
        this.Assert(AudioSettings.outputSampleRate > 0, "No audio device!");
    }
}

/// <summary>
/// Same pattern, but using a custom string-keyed channel registered via a
/// <see cref="LogChannelAsset"/> (see Quick Start &gt; Custom Channels in the README).
/// The id is just a string -- users can add as many as they like without modifying
/// the built-in <see cref="LogChannel"/> enum.
/// </summary>
[LogConfig(prefix = "Telemetry", channelId = "Telemetry", colorHex = "#7CB7FF")]
public class IJSLoggableTelemetryExample : MonoBehaviour, IIJSLoggable
{
    private void Start()
    {
        this.Log("Telemetry session started");
    }

    private void OnDestroy()
    {
        this.LogException(new InvalidOperationException("Example only"));
    }
}

/// <summary>
/// Direct (non-attribute) usage of the string-keyed channel API.
/// </summary>
public class CustomChannelDirectExample : MonoBehaviour
{
    private readonly IJSLogger _logger =
        IJSLogger.Create("Replay", Color.cyan, true, channelId: "Replay");

    private void Start() => _logger.PrintLog("Replay buffer initialized");
}
