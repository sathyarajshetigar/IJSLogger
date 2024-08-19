IJS Logger

To install
Select "Add package from git URL"
Paste this URL to your GitHub repository
https://github.com/sathyarajshetigar/IJSLogger.git#upm


Documentation for Logging Utility
Overview
This repository contains a logging utility designed for Unity projects, providing functionality to easily manage and customize log messages. This document describes the classes and methods available within the utility, explains how to use it, and provides examples for better understanding.
Classes and Methods
IJSLogger
The IJSLogger class provides various methods to enable, disable, and modify logging behavior in your Unity project.
Methods
void EnableUseLogs()
void DisableUseLogs()
void OnUseLogsChanged()
void UpdateScriptingDefineSymbols(string val, bool add)
IJSLogger(string prefix = "", Color? color = null, bool logsEnabled = true)
void ToggleLogs(bool enable)
void ModifyPrefix(string prefix)
void ModifyColor(Color color)
void PrintLog(string message, LogType logType = LogType.Log, GameObject go = null)
void Log(string message, LogType type = LogType.Log, GameObject go = null, Color? color = null)
For detailed implementation of each method, refer to IJSLogger.
 
When working with IJSLogger, you can refer to the example provided to understand how to integrate and use the logger in your Unity scripts.
Example Usage
File: IJSLoggerExample.cs

using UnityEngine;

namespace com.ijs.logger.samples
{
    public class IJSLoggerExample : MonoBehaviour
    {
        #region Logger

        private readonly IJSLogger _logger = new("IJSLoggerExample", Color.yellow);
        [SerializeField] private bool _enableLogs;
        [SerializeField] private Color _logColor;

        private void OnValidate()
        {
            _logger.ToggleLogs(_enableLogs);
            _logger.ModifyColor(_logColor);
        }

        #endregion

        private void Awake()
        {
            IJSLogger.Log("IJSLoggerExample Log");
            _logger.PrintLog("IJSLoggerExample PrintLog");
        }
    }
}


This example demonstrates how to:
Instantiate the IJSLogger with specific parameters
Toggle the logging state
Modify the log color
Use the Log and PrintLog methods to output log messages
Conclusion
This logging utility simplifies the process of managing log messages in Unity projects. By integrating IJSLogger, developers can gain better control over their logs, customize their appearance, and toggle logging on and off as needed. For more details, refer to the provided examples and method definitions.
Feel free to reach out if you have any questions or need further assistance. Happy coding!