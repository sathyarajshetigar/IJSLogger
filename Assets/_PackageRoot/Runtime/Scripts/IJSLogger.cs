#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif
using UnityEngine;
using Debug = UnityEngine.Debug;
using System;
using System.Diagnostics;
using System.Linq;

namespace com.ijs.logger
{
    /// <summary>
    /// The <c>IJSLogger</c> class is a utility class that provides logging capabilities in Unity.
    /// It allows you to print log messages with customizable prefixes, colors, and log types.
    /// </summary>
    public class IJSLogger
    {
#if UNITY_EDITOR
        private const string UseLogs = "USE_LOGS";
        private static bool useLogs;

        /// <summary>
        /// Enables or disables the use of logs in the IJSLogger class.
        /// This method sets a scripting define symbol based on the enable parameter value.
        /// </summary>
        [MenuItem("IJS/Logger/Enable Logs")]
        private static void EnableUseLogs()
        {
            useLogs = true;
            OnUseLogsChanged();
        }

        /// <summary>
        /// The <c>DisableUseLogs</c> method disables the use of logs in the Unity application.
        /// </summary>
        [MenuItem("IJS/Logger/Disable Logs")]
        private static void DisableUseLogs()
        {
            useLogs = false;
            OnUseLogsChanged();
        }

        /// <summary>
        /// The <c>IJSLogger</c> class is a utility class that provides logging capabilities in Unity.
        /// It allows you to print log messages with customizable prefixes, colors, and log types.
        /// </summary>
        private static void OnUseLogsChanged()
        {
            UpdateScriptingDefineSymbols(UseLogs, useLogs);
        }


        /// <summary>
        /// Updates the scripting define symbols based on the given value and whether to add or remove it.
        /// </summary>
        /// <param name="val">The value to add or remove from the scripting defines symbols.</param>
        /// <param name="add">A boolean indicating whether to add or remove the given value.</param>
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
        private bool _logsEnabled; // Whether to log
        private string _logPrefix; // Prefix for log messages

        /// The `IJSLogger` class is a utility class that provides logging capabilities in Unity.
        /// It allows you to print log messages with customizable prefixes, colors, and log types.
        /// /
        public IJSLogger(string prefix = "", Color? color = null, bool logsEnabled = true)
        {
            _logColor = color ?? Color.white;
            _logPrefix = prefix;
            _logsEnabled = logsEnabled;
        }

        /// <summary>
        /// ToggleLogs method is used to enable or disable logging in IJSLogger class.
        /// </summary>
        /// <param name="enable">A boolean value indicating whether to enable or disable logging.</param>
        public void ToggleLogs(bool enable)
        {
            _logsEnabled = enable;
        }

        /// <summary>
        /// Modifies the prefix for log messages.
        /// </summary>
        /// <param name="prefix">The new prefix for log messages.</param>
        public void ModifyPrefix(string prefix)
        {
            _logPrefix = prefix;
        }

        /// <summary>
        /// Modifies the color used for log messages.
        /// </summary>
        /// <param name="color">The new color for log messages.</param>
        public void ModifyColor(Color color)
        {
            _logColor = color;
        }

        /// <summary>
        /// Prints a log message with customizable prefixes, colors, and log types.
        /// </summary>
        /// <param name="message">The log message to print.</param>
        /// <param name="logType">The type of the log (LogType.Log by default).</param>
        /// <param name="go">The GameObject associated with the log message (null by default).</param>
        /// <remarks>
        /// This method is used to print log messages with customized prefixes, colors, and log types.
        /// The message is formatted with the specified prefix and then logged using Unity's Debug class.
        /// If logs are not enabled, the method does nothing.
        /// </remarks>
        [Conditional("USE_LOGS")]
        public void PrintLog(string message, LogType logType = LogType.Log, GameObject go = null)
        {
            if (!_logsEnabled) return;
            message = $"{_logPrefix}:: {message}";
            Log(message, logType, go, _logColor);
        }

        /// <summary>
        /// Allows logging messages with customizable prefixes, colors, and log types.
        /// </summary>
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


        // Debug.Log($"\n\n{message} : => firstMethod: {firstCallerMemberName}:secondMethod {secondCallerMemberName}\n");
        Debug.Log($"\n\n{message}");
#endif
        }
    }
}