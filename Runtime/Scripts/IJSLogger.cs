using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace com.ijs.logger
{
    public class IJSLogger
    {
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
            var stackTrace = new StackTrace(true);
            var callerFrames = GetRelevantStackFrames(stackTrace);

            // Get the method two steps back in the call stack, effectively skipping logger methods
            var firstCallerFrame = callerFrames.ElementAtOrDefault(0);
            var secondCallerFrame = callerFrames.ElementAtOrDefault(1);

            var firstCallerClassName = firstCallerFrame?.GetMethod()?.DeclaringType?.Name ?? "UnknownClass";
            var firstCallerMethodName = firstCallerFrame?.GetMethod()?.Name ?? "UnknownMethod";

            var secondCallerClassName = secondCallerFrame?.GetMethod()?.DeclaringType?.Name ?? "UnknownClass";
            var secondCallerMethodName = secondCallerFrame?.GetMethod()?.Name ?? "UnknownMethod";

            string logMessage = message;

            // Append current class and method only if they are not unknown
            if (firstCallerClassName != "UnknownClass" && firstCallerMethodName != "UnknownMethod")
                logMessage += $" ⇒ F: {firstCallerClassName}.{firstCallerMethodName}";

            // Append previous class and method only if they are not unknown
            if (secondCallerClassName != "UnknownClass" && secondCallerMethodName != "UnknownMethod")
                logMessage += $", S: {secondCallerClassName}.{secondCallerMethodName}";

            Debug.Log($"\n\n{logMessage}\n");
#endif
        }


        private static List<StackFrame> GetRelevantStackFrames(StackTrace stackTrace)
        {
            var frames = new List<StackFrame>();
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var methodName = frame?.GetMethod()?.Name;
                var className = frame?.GetMethod()?.DeclaringType?.Name;
                if (methodName != "MoveNext"
                    && methodName != "InvokeMoveNext"
                    && !(methodName == "Start" && className == "AsyncVoidMethodBuilder")
                    && methodName != "PrintLog"
                    && methodName != "RunInternal"
                    && methodName != "InvokeAction"
                    && methodName != "FinishContinuations"
                    && methodName != "Invoke"
                    && methodName != "ExecuteTasks"
                    && methodName != "RunCallback"
                    && methodName != "Exec"
                    && !(className?.StartsWith("AsyncTaskMethodBuilder") ?? false)
                    && !(className == "ExecutionContext" && methodName == "Run")
                    && !(className == "SynchronizationContextAwaitTaskContinuation" && methodName == "Run")
                    && !(className == "AwaitTaskContinuation" && methodName == "InvokeAction")
                    && !(className == "MoveNextRunner" && methodName == "Run")
                    && className != "IJSLogger"
                    && className != "Tween"
                    && className != "Clickable"
                    && className != "TaskCompletionSource"
                    && className != "TweenManager"
                    && className != "UnitySynchronizationContext"
                    && className != "Task"
                    && className != "InternalEditorUtility"
                    && className != "DOTweenComponent"
                    && className != "Socket"
                    && className != "Task`1"
                    && className != "<>c"
                    && !(className == "WorkRequest" && methodName == "Invoke")) // Ignore specific methods and classes
                {
                    frames.Add(frame);
                }
            }
            return frames;
        }
    }
}