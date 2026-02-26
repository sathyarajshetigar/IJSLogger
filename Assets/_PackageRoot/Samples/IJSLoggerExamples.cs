using UnityEngine;
using com.ijs.logger;

/// <summary>
/// Example usage of IJSLogger with all the new features.
/// Demonstrates channels, rate limiting, contexts, assertions, and conditional logging.
/// </summary>
public class IJSLoggerExamples : MonoBehaviour
{
    // Create loggers for different channels
    private readonly IJSLogger _gameplayLogger = new IJSLogger("Gameplay", Color.cyan, true, LogChannel.Gameplay);
    private readonly IJSLogger _audioLogger = new IJSLogger("Audio", Color.yellow, true, LogChannel.Audio);
    private readonly IJSLogger _networkLogger = new IJSLogger("Network", Color.green, true, LogChannel.Network);
    private readonly IJSLogger _performanceLogger = new IJSLogger("Perf", Color.magenta, true, LogChannel.Performance);

    [Header("Example Settings")]
    [SerializeField] private float health = 100f;
    [SerializeField] private int enemyCount = 5;
    [SerializeField] private bool debugMode = true;

    private void Start()
    {
        DemoBasicLogging();
        DemoChannelFiltering();
        DemoRateLimiting();
        DemoContexts();
        DemoAssertions();
        DemoConditionalLogging();
    }

    private void Update()
    {
        // Rate limiting in action - this won't spam the console
        _performanceLogger.LogThrottled($"FPS: {1f / Time.deltaTime:F1}", 1.0f);
    }

    /// <summary>
    /// Basic logging examples with different channels.
    /// </summary>
    private void DemoBasicLogging()
    {
        _gameplayLogger.PrintLog("Game started!");
        _audioLogger.PrintLog("Audio system initialized", LogType.Log);
        _networkLogger.PrintLog("Connected to server", LogType.Log);
    }

    /// <summary>
    /// Demonstrates how channel filtering works.
    /// You can enable/disable channels in Window -> IJS Logger -> Settings
    /// </summary>
    private void DemoChannelFiltering()
    {
        // These logs will only appear if their respective channels are enabled
        _audioLogger.PrintLog("Playing background music");
        _networkLogger.PrintLog("Sending player position update");
        _performanceLogger.PrintLog("Frame render time: 16ms");

        // You can also use different log types
        _gameplayLogger.PrintLog("Player took damage", LogType.Warning);
    }

    /// <summary>
    /// Demonstrates rate limiting to prevent log spam.
    /// </summary>
    private void DemoRateLimiting()
    {
        // This will log immediately, then suppress logs for 2 seconds
        for (int i = 0; i < 10; i++)
        {
            _gameplayLogger.LogThrottled("This message is rate-limited", 2.0f);
        }

        // After 2 seconds, it will log again showing how many were suppressed
    }

    /// <summary>
    /// Demonstrates using log contexts for organized logging.
    /// </summary>
    private void DemoContexts()
    {
        using (new LogContext("Level Loading"))
        {
            _gameplayLogger.PrintLog("Loading assets");
            _gameplayLogger.PrintLog("Spawning enemies");

            using (new LogContext("Enemy Spawner"))
            {
                // Nested contexts work too!
                _gameplayLogger.PrintLog($"Spawned {enemyCount} enemies");
            }

            _gameplayLogger.PrintLog("Level load complete");
        }

        // Context is removed after the using block
        _gameplayLogger.PrintLog("Ready to play");
    }

    /// <summary>
    /// Demonstrates the assertion system with fluent API.
    /// </summary>
    private void DemoAssertions()
    {
        // Simple assertion
        _gameplayLogger.Assert(health > 0, "Health must be positive!");

        // Assertion with callback on failure
        _gameplayLogger.Assert(enemyCount <= 100, "Too many enemies!")
            .OnFailure(() => enemyCount = 100);

        // Validate method parameters
        _gameplayLogger.ValidateNotNull(gameObject, nameof(gameObject));
        _gameplayLogger.ValidateRange(health, 0, 100, nameof(health));

        // Assert with editor pause (only in editor)
        _gameplayLogger.Assert(enemyCount > 0, "No enemies spawned!")
#if UNITY_EDITOR
            .PauseEditor();
#else
            .BreakDebugger();
#endif
    }

    /// <summary>
    /// Demonstrates different conditional logging patterns.
    /// </summary>
    private void DemoConditionalLogging()
    {
        // Log only if condition is true
        _gameplayLogger.LogIf(debugMode, "Debug mode is enabled");

        // Log only if health is low
        _gameplayLogger.LogIf(health < 20, "Low health warning!", LogType.Warning);

        // Lazy evaluation - expensive string building only happens if condition is true
        _gameplayLogger.LogIf(
            () => debugMode && health < 50,
            () => $"Debug Info - Health: {health}, Enemies: {enemyCount}, Position: {transform.position}",
            LogType.Log
        );
    }

    /// <summary>
    /// Example of logging in different contexts (gameplay scenario).
    /// </summary>
    private void SimulateGameplayScenario()
    {
        using (new LogContext("Combat"))
        {
            _gameplayLogger.PrintLog("Player entered combat");

            // Simulate taking damage
            var damage = 25f;
            health -= damage;
            _gameplayLogger.PrintLog($"Player took {damage} damage. Health: {health}", LogType.Warning);

            // Assert health is valid
            _gameplayLogger.Assert(health >= 0, "Health cannot be negative!")
                .OnFailure(() => health = 0);

            // Conditional logging for critical health
            _gameplayLogger.LogIf(health <= 20, "CRITICAL HEALTH!", LogType.Error);
        }
    }

    /// <summary>
    /// Example of performance monitoring with rate limiting.
    /// </summary>
    private void MonitorPerformance()
    {
        // Log performance metrics with throttling
        _performanceLogger.LogThrottled($"Memory: {System.GC.GetTotalMemory(false) / 1024 / 1024}MB", 5.0f);
        _performanceLogger.LogThrottled($"Objects: {FindObjectsOfType<GameObject>().Length}", 5.0f);
    }

    /// <summary>
    /// Example of network logging with contexts.
    /// </summary>
    private void SimulateNetworkActivity()
    {
        using (new LogContext("Network"))
        {
            _networkLogger.PrintLog("Connecting to server...");

            using (new LogContext("Authentication"))
            {
                _networkLogger.PrintLog("Sending credentials");
                _networkLogger.PrintLog("Authentication successful", LogType.Log);
            }

            _networkLogger.PrintLog("Connection established");
        }
    }

    /// <summary>
    /// Example of using multiple features together.
    /// </summary>
    private void ComplexExample()
    {
        using (new LogContext("Player Update"))
        {
            // Rate-limited position update
            _gameplayLogger.LogThrottled($"Position: {transform.position}", 0.5f);

            // Conditional logging with lazy evaluation
            _performanceLogger.LogIf(
                () => Time.deltaTime > 0.033f, // If frame took > 33ms
                () => $"Slow frame detected: {Time.deltaTime * 1000:F1}ms",
                LogType.Warning
            );

            // Validation
            _gameplayLogger.ValidateRange(health, 0, 100, nameof(health));

            // Assertion with action
            _gameplayLogger.Assert(transform.position.y > -100, "Player fell through world!")
                .OnFailure(() => transform.position = Vector3.zero);
        }
    }
}
