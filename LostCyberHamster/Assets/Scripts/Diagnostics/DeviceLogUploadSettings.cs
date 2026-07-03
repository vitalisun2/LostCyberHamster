using System;
using UnityEngine;

namespace Assets.Scripts.Diagnostics
{
    [Serializable]
    public sealed class DeviceLogUploadSettings
    {
        private const int _defaultTimeoutSeconds = 10;
        private const int _defaultMaxLogBytes = 1024 * 1024;

        public bool enabled;
        public bool allowInEditor;
        public bool allowOnAndroid;
        public bool allowOnOtherPlatforms;
        public string endpointUrl;
        public string sharedToken;
        public string buildLabel;
        public string branch;
        public string shortSha;
        public bool dirty;
        public int uploadTimeoutSeconds;
        public int maxLogBytes;

        public bool HasEndpoint => !string.IsNullOrWhiteSpace(endpointUrl);
        public int UploadTimeoutSeconds => uploadTimeoutSeconds > 0 ? uploadTimeoutSeconds : _defaultTimeoutSeconds;
        public int MaxLogBytes => maxLogBytes > 0 ? maxLogBytes : _defaultMaxLogBytes;

        public bool IsPlatformAllowed()
        {
            if (Application.isEditor)
            {
                return allowInEditor;
            }

            return Application.platform == RuntimePlatform.Android
                ? allowOnAndroid
                : allowOnOtherPlatforms;
        }
    }
}
