using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace com.ijs.logger
{
    /// <summary>
    /// Editor window for viewing and filtering all Unity logs with special highlighting for IJSLogger logs.
    /// </summary>
    public class IJSLogViewerWindow : EditorWindow
    {
        [Serializable]
        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public double timestamp;
            public bool isIJSLogger;

            public LogEntry(string message, string stackTrace, LogType type)
            {
                this.message = message;
                this.stackTrace = stackTrace;
                this.type = type;
                this.timestamp = EditorApplication.timeSinceStartup;
                this.isIJSLogger = IsIJSLoggerLog(message);
            }

            private static bool IsIJSLoggerLog(string message)
            {
                // Check if message contains IJSLogger formatting patterns
                return message.Contains("<size=11>") ||
                       message.Contains(":: ") ||
                       Regex.IsMatch(message, @"\[.*?\].*?::");
            }
        }

        private List<LogEntry> _logs = new List<LogEntry>();
        private HashSet<LogEntry> _selectedLogs = new HashSet<LogEntry>();
        private LogEntry _lastClickedLog;
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showLogs = true;
        private bool _showWarnings = true;
        private bool _showErrors = true;
        private bool _showIJSLoggerOnly = false;
        private bool _autoScroll = true;
        private bool _showTimestamp = true;
        private int _maxLogs = 1000;

        private GUIStyle _logStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _ijsLoggerStyle;
        private GUIStyle _selectedStyle;
        private bool _stylesInitialized;

        [MenuItem("Window/IJS Logger/Log Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<IJSLogViewerWindow>("IJS Log Viewer");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
            _stylesInitialized = false;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized)
                return;

            _logStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                fontSize = 11
            };

            _warningStyle = new GUIStyle(_logStyle);
            _warningStyle.normal.textColor = new Color(0.8f, 0.6f, 0f);

            _errorStyle = new GUIStyle(_logStyle);
            _errorStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);

            _ijsLoggerStyle = new GUIStyle(_logStyle);
            _ijsLoggerStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.3f, 0.4f, 0.2f));
            _ijsLoggerStyle.padding = new RectOffset(5, 5, 3, 3);

            _selectedStyle = new GUIStyle(EditorStyles.helpBox);
            _selectedStyle.normal.background = MakeTexture(2, 2, new Color(0.17f, 0.36f, 0.53f, 0.7f));
            _selectedStyle.padding = new RectOffset(5, 5, 3, 3);

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void HandleLog(string message, string stackTrace, LogType type)
        {
            var entry = new LogEntry(message, stackTrace, type);
            _logs.Add(entry);

            // Limit log count
            if (_logs.Count > _maxLogs)
            {
                _logs.RemoveAt(0);
            }

            if (_autoScroll)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            DrawToolbar();
            DrawLogList();
        }

        private void DrawToolbar()
        {
            // Row 1: Filter toggles
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _logs.Clear();
                _selectedLogs.Clear();
                _lastClickedLog = null;
            }

            GUILayout.Space(10);

            var logColor = _showLogs ? Color.white : Color.gray;
            GUI.color = logColor;
            _showLogs = GUILayout.Toggle(_showLogs, $"Logs ({CountLogsByType(LogType.Log)})",
                EditorStyles.toolbarButton, GUILayout.Width(80));

            var warningColor = _showWarnings ? Color.yellow : Color.gray;
            GUI.color = warningColor;
            _showWarnings = GUILayout.Toggle(_showWarnings, $"Warnings ({CountLogsByType(LogType.Warning)})",
                EditorStyles.toolbarButton, GUILayout.Width(100));

            var errorColor = _showErrors ? Color.red : Color.gray;
            GUI.color = errorColor;
            _showErrors = GUILayout.Toggle(_showErrors, $"Errors ({CountLogsByType(LogType.Error)})",
                EditorStyles.toolbarButton, GUILayout.Width(90));

            GUI.color = Color.white;

            GUILayout.Space(10);

            var ijsColor = _showIJSLoggerOnly ? new Color(0.5f, 0.8f, 1f) : Color.white;
            GUI.color = ijsColor;
            _showIJSLoggerOnly = GUILayout.Toggle(_showIJSLoggerOnly, "IJSLogger Only",
                EditorStyles.toolbarButton, GUILayout.Width(100));
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            _showTimestamp = GUILayout.Toggle(_showTimestamp, "Timestamp",
                EditorStyles.toolbarButton, GUILayout.Width(75));

            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll",
                EditorStyles.toolbarButton, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();

            // Row 2: Search
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            // Row 3: Settings
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Max Logs: {_maxLogs}", GUILayout.Width(110));
            GUILayout.Space(4);
            _maxLogs = (int)GUILayout.HorizontalSlider(_maxLogs, 100, 5000, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Export / Copy ▾", EditorStyles.toolbarDropDown, GUILayout.Width(110)))
            {
                var menu = new GenericMenu();
                bool hasSelection = _selectedLogs.Count > 0;

                menu.AddItem(new GUIContent("Copy All Logs"), false, () => CopyLogsToClipboard(false, false));
                menu.AddItem(new GUIContent("Copy All Logs + Stack Traces"), false, () => CopyLogsToClipboard(true, false));
                menu.AddSeparator("");

                if (hasSelection)
                {
                    menu.AddItem(new GUIContent($"Copy Selected ({_selectedLogs.Count})"), false, () => CopyLogsToClipboard(false, true));
                    menu.AddItem(new GUIContent($"Copy Selected + Stack Traces ({_selectedLogs.Count})"), false, () => CopyLogsToClipboard(true, true));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Copy Selected (none selected)"));
                    menu.AddDisabledItem(new GUIContent("Copy Selected + Stack Traces (none selected)"));
                }

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Export All to File"), false, () => ExportLogs(true, false));
                menu.AddItem(new GUIContent("Export All to File (no Stack Traces)"), false, () => ExportLogs(false, false));
                menu.AddSeparator("");

                if (hasSelection)
                {
                    menu.AddItem(new GUIContent($"Export Selected to File ({_selectedLogs.Count})"), false, () => ExportLogs(true, true));
                    menu.AddItem(new GUIContent($"Export Selected to File (no Stack Traces) ({_selectedLogs.Count})"), false, () => ExportLogs(false, true));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Export Selected to File (none selected)"));
                    menu.AddDisabledItem(new GUIContent("Export Selected to File (no Stack Traces) (none selected)"));
                }

                menu.ShowAsContext();
            }

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                IJSLoggerSettingsWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogList()
        {
            var filteredLogs = GetFilteredLogs();

            EditorGUILayout.BeginVertical();

            // Detect manual scroll to auto-disable auto-scroll
            if (_autoScroll && Event.current.type == EventType.ScrollWheel && Event.current.delta.y < 0)
            {
                _autoScroll = false;
                Repaint();
            }

            var previousScroll = _scrollPosition;
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (filteredLogs.Count == 0)
            {
                EditorGUILayout.HelpBox("No logs to display. Logs will appear here as your game runs.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < filteredLogs.Count; i++)
                {
                    DrawLogEntry(filteredLogs[i], filteredLogs, i);
                }

                // Auto-scroll to bottom
                if (_autoScroll && Event.current.type == EventType.Repaint)
                {
                    _scrollPosition.y = float.MaxValue;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawLogEntry(LogEntry log, List<LogEntry> filteredLogs, int index)
        {
            var style = GetStyleForLog(log);
            bool isSelected = _selectedLogs.Contains(log);
            var boxStyle = isSelected ? _selectedStyle : (log.isIJSLogger ? _ijsLoggerStyle : EditorStyles.helpBox);

            var rect = EditorGUILayout.BeginHorizontal(boxStyle, GUILayout.MinHeight(22));

            // Handle click selection (left mouse button only)
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                var evt = Event.current;
                if (evt.shift && _lastClickedLog != null)
                {
                    // Shift-click: range select
                    int lastIndex = filteredLogs.IndexOf(_lastClickedLog);
                    if (lastIndex >= 0)
                    {
                        int from = Mathf.Min(lastIndex, index);
                        int to = Mathf.Max(lastIndex, index);
                        if (!evt.command && !evt.control)
                            _selectedLogs.Clear();
                        for (int i = from; i <= to; i++)
                            _selectedLogs.Add(filteredLogs[i]);
                    }
                }
                else if (evt.command || evt.control)
                {
                    // Cmd/Ctrl-click: toggle individual
                    if (_selectedLogs.Contains(log))
                        _selectedLogs.Remove(log);
                    else
                        _selectedLogs.Add(log);
                }
                else
                {
                    // Normal click: single select
                    _selectedLogs.Clear();
                    _selectedLogs.Add(log);
                }
                _lastClickedLog = log;
                evt.Use();
                Repaint();
            }

            // Timestamp
            if (_showTimestamp)
            {
                var timeStr = TimeSpan.FromSeconds(log.timestamp).ToString(@"hh\:mm\:ss\.fff");
                EditorGUILayout.LabelField(timeStr, GUILayout.Width(100));
            }

            // Type icon
            var typeIcon = GetIconForLogType(log.type);
            EditorGUILayout.LabelField(new GUIContent(typeIcon), GUILayout.Width(20));

            // IJS tag
            if (log.isIJSLogger)
            {
                EditorGUILayout.LabelField("[IJS]", EditorStyles.boldLabel, GUILayout.Width(40));
            }

            // Message — use a label so the text is always visible
            EditorGUILayout.LabelField(log.message, style);

            EditorGUILayout.EndHorizontal();

            // Stack trace (collapsible) for errors
            if (!string.IsNullOrEmpty(log.stackTrace) && (log.type == LogType.Error || log.type == LogType.Exception))
            {
                if (GUILayout.Button("Show Stack Trace", EditorStyles.miniButton))
                {
                    Debug.Log($"Stack Trace:\n{log.stackTrace}");
                }
            }

            EditorGUILayout.Space(1);
        }

        private GUIStyle GetStyleForLog(LogEntry log)
        {
            switch (log.type)
            {
                case LogType.Warning:
                    return _warningStyle;
                case LogType.Error:
                case LogType.Exception:
                    return _errorStyle;
                default:
                    return _logStyle;
            }
        }

        private string GetIconForLogType(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    return "⊗";
                case LogType.Warning:
                    return "⚠";
                default:
                    return "ℹ";
            }
        }

        private List<LogEntry> GetFilteredLogs()
        {
            var filtered = new List<LogEntry>();

            foreach (var log in _logs)
            {
                // Type filter
                if (log.type == LogType.Log && !_showLogs)
                    continue;
                if (log.type == LogType.Warning && !_showWarnings)
                    continue;
                if ((log.type == LogType.Error || log.type == LogType.Exception) && !_showErrors)
                    continue;

                // IJSLogger filter
                if (_showIJSLoggerOnly && !log.isIJSLogger)
                    continue;

                // Search filter
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !log.message.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase).Equals(-1) == false)
                {
                    continue;
                }

                filtered.Add(log);
            }

            return filtered;
        }

        private int CountLogsByType(LogType type)
        {
            int count = 0;
            foreach (var log in _logs)
            {
                if (log.type == type)
                    count++;
            }
            return count;
        }

        private List<LogEntry> GetLogsForAction(bool selectedOnly)
        {
            if (selectedOnly)
            {
                // Return selected logs in the order they appear in filtered list
                var filteredLogs = GetFilteredLogs();
                return filteredLogs.Where(log => _selectedLogs.Contains(log)).ToList();
            }
            return GetFilteredLogs();
        }

        private void CopyLogsToClipboard(bool includeStackTrace, bool selectedOnly)
        {
            var logs = GetLogsForAction(selectedOnly);
            if (logs.Count == 0)
            {
                ShowNotification(new GUIContent(selectedOnly ? "No logs selected" : "No logs to copy"));
                return;
            }

            var content = new System.Text.StringBuilder();
            foreach (var log in logs)
            {
                var timeStr = TimeSpan.FromSeconds(log.timestamp).ToString(@"hh\:mm\:ss\.fff");
                var cleanMessage = Regex.Replace(log.message, "<.*?>", "");
                content.AppendLine($"[{timeStr}] [{log.type}] {cleanMessage}");
                if (includeStackTrace && !string.IsNullOrEmpty(log.stackTrace))
                {
                    content.AppendLine(log.stackTrace);
                }
            }

            GUIUtility.systemCopyBuffer = content.ToString();
            ShowNotification(new GUIContent($"Copied {logs.Count} logs"));
        }

        private void ExportLogs(bool includeStackTrace, bool selectedOnly)
        {
            var logs = GetLogsForAction(selectedOnly);
            var path = EditorUtility.SaveFilePanel("Export Logs", "", $"IJSLogs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt", "txt");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var content = new System.Text.StringBuilder();
                content.AppendLine($"IJS Logger - Log Export{(selectedOnly ? " (Selected)" : "")}");
                content.AppendLine($"Exported: {DateTime.Now}");
                content.AppendLine($"Total Logs: {logs.Count}");
                content.AppendLine(new string('=', 80));
                content.AppendLine();

                foreach (var log in logs)
                {
                    var timeStr = TimeSpan.FromSeconds(log.timestamp).ToString(@"hh\:mm\:ss\.fff");
                    content.AppendLine($"[{timeStr}] [{log.type}] {log.message}");
                    if (includeStackTrace && !string.IsNullOrEmpty(log.stackTrace))
                    {
                        content.AppendLine($"Stack Trace:\n{log.stackTrace}");
                    }
                    content.AppendLine(new string('-', 80));
                }

                System.IO.File.WriteAllText(path, content.ToString());
                EditorUtility.DisplayDialog("Export Complete", $"Logs exported to:\n{path}", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Failed to export logs:\n{ex.Message}", "OK");
            }
        }
    }
}
