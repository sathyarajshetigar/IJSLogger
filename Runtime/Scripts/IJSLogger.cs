#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using System.Runtime.CompilerServices;
#endif
using UnityEngine;
using Debug = UnityEngine.Debug;
using System;
using System.Diagnostics;
using System.Linq;

namespace com.ijs.logger
{
    public class IJSLogger
    {
#if UNITY_EDITOR
        private const string UseLogs = "USE_LOGS";
        private static bool _useLogs;

        [MenuItem("IJS/Logger/Enable Logs")]
        private static void EnableUseLogs()
        {
            _useLogs = true;
            OnUseLogsChanged();
        }

        [MenuItem("IJS/Logger/Disable Logs")]
        private static void DisableUseLogs()
        {
            _useLogs = false;
            OnUseLogsChanged();
        }

        private static void OnUseLogsChanged()
        {
            UpdateScriptingDefineSymbols(UseLogs, _useLogs);
        }

        private static void UpdateScriptingDefineSymbols(string val, bool add)
        {
#if UNITY_EDITOR
        var platform = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        var definesString = PlayerSettings.GetScriptingDefineSymbols(platform);
        var allDefines = definesString.Split(';').ToList();
        if (add)
        {
            if (!allDefines.Contains(val))
                allDefines.Add(val);
        }
        else
        {
            if (allDefines.Contains(val))
                allDefines.Remove(val);
        }
        PlayerSettings.SetScriptingDefineSymbols(platform, string.Join(";", allDefines.ToArray()));
#endif
        }
#endif
        
        private Color _logColor; // Color for log messages
        private bool _logsEnabled; // Whether or not to log
        private string _logPrefix; // Prefix for log messages

        public IJSLogger(string prefix = "", Color? color = null, bool logsEnabled = true)
        {
            _logColor = color ?? Color.white;
            _logPrefix = prefix;
            _logsEnabled = logsEnabled;
        }

        public void ToggleLogs(bool enable)
        {
            _logsEnabled = enable;
        }

        public void ModifyPrefix(string prefix)
        {
            _logPrefix = prefix;
        }

        public void ModifyColor(Color color)
        {
            _logColor = color;
        }

        [Conditional("USE_LOGS")]
        public void PrintLog(string message, LogType logType = LogType.Log, GameObject go = null)
        {
            if (!_logsEnabled) return;
            message = $"{_logPrefix}:: {message}";
            Log(message, logType, go, _logColor);
        }

        [Conditional("USE_LOGS")]
        public static void Log(
            string message,
            LogType type = LogType.Log,
            GameObject go = null,
            Color? color = null)
        {
            color = color ?? Color.white;
#if UNITY_EDITOR
            var splitWords = message.Split();

            for (var i = 0; i < splitWords.Length; i++)
            {
                if (int.TryParse(splitWords[i], out _))
                {
                    splitWords[i] = $"<size=13><color=#FF214C>{splitWords[i]} </color></size>";
                }
            }

            var updatedText = string.Join(" ", splitWords);

            var formattedMessage =
                $"<size=11><i><b><color=#{(byte)(color.Value.r * 255f):X2}{(byte)(color.Value.g * 255f):X2}{(byte)(color.Value.b * 255f):X2}>{updatedText}</color></b></i></size>";

            switch (type)
            {
                case LogType.Error:
                    Debug.LogError(formattedMessage, go);
                    break;
                case LogType.Assert:
                    Debug.LogAssertion(formattedMessage, go);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(formattedMessage, go);
                    break;
                case LogType.Log:
                    Debug.Log(formattedMessage, go);
                    break;
                case LogType.Exception:
                    Debug.LogException(new Exception(formattedMessage), go);
                    break;
            }
#else
            var stackTrace = new StackTrace();

            var firstCallerMemberName = stackTrace.GetFrame(1).GetMethod().Name;
            // var firstCallerSourceLineNumber = firstCaller.GetFileLineNumber();

            var secondCallerMemberName = stackTrace.GetFrame(2).GetMethod().Name;

             // var thirdCallerMemberName = stackTrace.GetFrame(3).GetMethod().Name;


        // Debug.Log($"\n\n{message} : => firstMethod: {firstCallerMemberName} : secondMethod {secondCallerMemberName}\n");
        Debug.Log($"\n\n{message}");
#endif
        }
    }
}