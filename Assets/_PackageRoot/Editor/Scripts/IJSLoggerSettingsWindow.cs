using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace com.ijs.logger
{
    /// <summary>
    /// Editor window for managing IJSLogger settings and channel configurations.
    /// </summary>
    public class IJSLoggerSettingsWindow : EditorWindow
    {
        private IJSLoggerSettings _settings;
        private Vector2 _scrollPosition;
        private int _selectedTab;
        private readonly string[] _tabNames = { "Channel Settings", "About" };

        [MenuItem("Window/IJS Logger/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<IJSLoggerSettingsWindow>("IJS Logger Settings");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadOrCreateSettings();
        }

        private void LoadOrCreateSettings()
        {
            _settings = IJSLoggerSettings.Instance;

            if (_settings == null)
            {
                // Prompt to create settings
                if (EditorUtility.DisplayDialog("IJSLogger Settings",
                    "No IJSLoggerSettings asset found. Would you like to create one?",
                    "Create", "Cancel"))
                {
                    CreateSettingsAsset();
                }
            }
        }

        private void CreateSettingsAsset()
        {
            // Create Resources folder if it doesn't exist
            const string resourcesPath = "Assets/_PackageRoot/Runtime/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                var folders = resourcesPath.Split('/');
                var currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    var newPath = $"{currentPath}/{folders[i]}";
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }

            // Create settings asset
            _settings = CreateInstance<IJSLoggerSettings>();
            _settings.InitializeDefaults();

            AssetDatabase.CreateAsset(_settings, $"{resourcesPath}/IJSLoggerSettings.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[IJSLogger] Created settings asset at {resourcesPath}/IJSLoggerSettings.asset");
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("No IJSLoggerSettings found. Please create one.", MessageType.Warning);
                if (GUILayout.Button("Create Settings Asset"))
                {
                    CreateSettingsAsset();
                }
                return;
            }

            // Header
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("IJS Logger Settings", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);

            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case 0:
                    DrawChannelSettings();
                    break;
                case 1:
                    DrawAboutTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // Footer
            EditorGUILayout.Space();
            DrawFooter();
        }

        private void DrawChannelSettings()
        {
            EditorGUILayout.LabelField("Channel Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure which channels are enabled and where they log.\n\n" +
                "• Editor Only: Logs only in Unity Editor\n" +
                "• Build Only: Logs only in builds (not editor)\n" +
                "• Both: Logs in both editor and builds",
                MessageType.Info);

            EditorGUILayout.Space();

            // Quick actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All"))
            {
                EnableAllChannels(true);
            }
            if (GUILayout.Button("Disable All"))
            {
                EnableAllChannels(false);
            }
            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Are you sure you want to reset all channel settings to defaults?",
                    "Reset", "Cancel"))
                {
                    _settings.InitializeDefaults();
                    EditorUtility.SetDirty(_settings);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Channel list
            var serializedObject = new SerializedObject(_settings);
            serializedObject.Update();

            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                if (channel == LogChannel.Default)
                    continue;

                DrawChannelRow(channel);
            }

            // -------- Custom string-keyed channels (LogChannelAsset) --------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Custom Channels", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag LogChannelAsset assets into the 'customChannels' list on the settings asset, " +
                "or call IJSLoggerSettings.RegisterCustomChannel(asset) at startup. " +
                "Channels listed here are filtered by their string Id.",
                MessageType.None);

            var customChannels = _settings.CustomChannels;
            if (customChannels != null)
            {
                for (var i = 0; i < customChannels.Count; i++)
                {
                    var asset = customChannels[i];
                    if (asset == null) continue;
                    DrawCustomChannelRow(asset);
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_settings);
            }
        }

        private void DrawChannelRow(LogChannel channel)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Channel name
            var config = _settings.GetChannelConfig(channel);
            var isEnabled = config?.enabled ?? true;
            var scope = config?.scope ?? ChannelScope.Both;

            var newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));
            EditorGUILayout.LabelField(channel.ToString(), GUILayout.Width(120));

            // Scope selection
            var newScope = (ChannelScope)EditorGUILayout.EnumPopup(scope, GUILayout.Width(100));

            // Status indicator
            var statusColor = GetStatusColor(newEnabled, newScope);
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = statusColor;
            EditorGUILayout.LabelField(GetStatusText(newEnabled, newScope), EditorStyles.helpBox, GUILayout.Width(100));
            GUI.backgroundColor = oldColor;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Update if changed
            if (newEnabled != isEnabled)
            {
                _settings.SetChannelEnabled(channel, newEnabled);
            }
            if (newScope != scope)
            {
                _settings.SetChannelScope(channel, newScope);
            }
        }

        private void DrawCustomChannelRow(LogChannelAsset asset)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var id = asset.Id;
            var config = _settings.GetChannelConfig(id);
            var isEnabled = config?.enabled ?? asset.DefaultEnabled;
            var scope = config?.scope ?? asset.DefaultScope;

            var newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));
            EditorGUILayout.LabelField(id, GUILayout.Width(120));
            var newScope = (ChannelScope)EditorGUILayout.EnumPopup(scope, GUILayout.Width(100));

            var statusColor = GetStatusColor(newEnabled, newScope);
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = statusColor;
            EditorGUILayout.LabelField(GetStatusText(newEnabled, newScope), EditorStyles.helpBox, GUILayout.Width(100));
            GUI.backgroundColor = oldColor;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (newEnabled != isEnabled) _settings.SetChannelEnabled(id, newEnabled);
            if (newScope != scope) _settings.SetChannelScope(id, newScope);
        }

        private Color GetStatusColor(bool enabled, ChannelScope scope)
        {
            if (!enabled)
                return new Color(1f, 0.5f, 0.5f); // Red tint for disabled

#if UNITY_EDITOR
            if (scope == ChannelScope.EditorOnly || scope == ChannelScope.Both)
                return new Color(0.5f, 1f, 0.5f); // Green tint for active
            else
                return new Color(1f, 1f, 0.5f); // Yellow tint for build only
#else
            if (scope == ChannelScope.BuildOnly || scope == ChannelScope.Both)
                return new Color(0.5f, 1f, 0.5f); // Green tint for active
            else
                return new Color(1f, 1f, 0.5f); // Yellow tint for editor only
#endif
        }

        private string GetStatusText(bool enabled, ChannelScope scope)
        {
            if (!enabled)
                return "Disabled";

#if UNITY_EDITOR
            if (scope == ChannelScope.EditorOnly || scope == ChannelScope.Both)
                return "Active";
            else
                return "Build Only";
#else
            if (scope == ChannelScope.BuildOnly || scope == ChannelScope.Both)
                return "Active";
            else
                return "Editor Only";
#endif
        }

        private void EnableAllChannels(bool enable)
        {
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                if (channel == LogChannel.Default)
                    continue;

                _settings.SetChannelEnabled(channel, enable);
            }
            EditorUtility.SetDirty(_settings);
        }

        private void DrawAboutTab()
        {
            EditorGUILayout.LabelField("About IJS Logger", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "IJS Logger - Advanced Unity Logging System\n\n" +
                "Features:\n" +
                "• Channel-based filtering\n" +
                "• Colored log output\n" +
                "• Rate limiting\n" +
                "• Log contexts and scopes\n" +
                "• Conditional logging\n" +
                "• Assert system\n" +
                "• Build-time log stripping\n\n" +
                "Version: 1.1.0",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Quick Start", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Create a logger instance:\n" +
                "   var logger = IJSLogger.Create(\"MyClass\", Color.cyan, true, LogChannel.Gameplay);\n\n" +
                "2. Log messages:\n" +
                "   logger.PrintLog(\"Hello World\");\n\n" +
                "3. Use advanced features:\n" +
                "   logger.LogThrottled(\"Spam message\", 1.0f);\n" +
                "   logger.Assert(health > 0, \"Health must be positive\");",
                MessageType.None);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            if (_settings != null)
            {
                if (GUILayout.Button("Open Settings Asset"))
                {
                    Selection.activeObject = _settings;
                    EditorGUIUtility.PingObject(_settings);
                }
            }

            if (GUILayout.Button("Open Log Viewer"))
            {
                IJSLogViewerWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
