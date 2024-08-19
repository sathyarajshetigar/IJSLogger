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