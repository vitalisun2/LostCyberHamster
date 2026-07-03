using System;
using System.Collections;
using System.IO;
using System.Text;
using GameManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Diagnostics
{
    public static class DeviceLogUploader
    {
        private const string _settingsResourcePath = "Diagnostics/device_log_settings";
        private const string _tokenHeaderName = "X-LCH-Device-Log-Token";
        private const string _ngrokSkipBrowserWarningHeaderName = "ngrok-skip-browser-warning";
        private const string _diagnosticLogFileName = "diagnostic_log.txt";
        private const string _diagnosticLogEncoding = "utf-8";

        private static readonly string _sessionStartedAtUtc = DateTime.UtcNow.ToString("O");
        public static void UploadDiagnosticLog(string reason)
        {
            DeviceLogUploadRunner.Enqueue(reason);
        }

        internal static IEnumerator UploadDiagnosticLogCoroutine(string reason)
        {
            if (!TryPrepareUpload(reason, out var settings, out var json))
            {
                yield break;
            }

            yield return SendHealthProbeCoroutine(settings, reason);
            yield return SendPayloadCoroutine(settings, json, reason);
        }

        private static bool TryPrepareUpload(string reason, out DeviceLogUploadSettings settings, out string json)
        {
            settings = null;
            json = null;

            try
            {
                settings = LoadSettings();
                if (!ShouldUpload(settings))
                {
                    return false;
                }

                var payload = BuildPayload(settings, reason);
                json = JsonUtility.ToJson(payload);
                return true;
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability(
                    $"[DEVICE LOG] upload prepare failed reason={reason} exception={exception.GetType().Name}: {exception.Message}");
                return false;
            }
        }

        private static DeviceLogUploadSettings LoadSettings()
        {
            var asset = Resources.Load<TextAsset>(_settingsResourcePath);
            if (asset == null)
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<DeviceLogUploadSettings>(asset.text);
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability(
                    $"[DEVICE LOG] failed to parse settings exception={exception.GetType().Name}: {exception.Message}");
                return null;
            }
        }

        private static bool ShouldUpload(DeviceLogUploadSettings settings)
        {
            bool shouldUpload = settings != null
                && settings.enabled
                && settings.HasEndpoint
                && settings.IsPlatformAllowed();

            if (!shouldUpload)
            {
                DebugManager.DiagStability(
                    $"[DEVICE LOG] upload disabled settingsNull={settings == null} " +
                    $"enabled={settings?.enabled} hasEndpoint={settings?.HasEndpoint} " +
                    $"platformAllowed={settings?.IsPlatformAllowed()}");
            }

            return shouldUpload;
        }

        private static DeviceLogUploadPayload BuildPayload(DeviceLogUploadSettings settings, string reason)
        {
            var logBytes = ReadDiagnosticLogBytes(settings.MaxLogBytes, out bool truncated);
            return new DeviceLogUploadPayload
            {
                metadata = new DeviceLogUploadMetadata
                {
                    sessionId = SystemInfo.deviceUniqueIdentifier,
                    reason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason,
                    createdAtUtc = DateTime.UtcNow.ToString("O"),
                    sessionStartedAtUtc = _sessionStartedAtUtc,
                    platform = Application.platform.ToString(),
                    unityVersion = Application.unityVersion,
                    appVersion = Application.version,
                    deviceModel = SystemInfo.deviceModel,
                    operatingSystem = SystemInfo.operatingSystem,
                    internetReachability = Application.internetReachability.ToString(),
                    buildLabel = settings.buildLabel,
                    branch = settings.branch,
                    shortSha = settings.shortSha,
                    dirty = settings.dirty,
                    endpointUrl = settings.endpointUrl,
                    activeScene = SceneManager.GetActiveScene().name,
                    currentLevel = GameDataManager.PlayerData?.CurrentLevel
                },
                diagnosticLogFileName = _diagnosticLogFileName,
                diagnosticLogEncoding = _diagnosticLogEncoding,
                diagnosticLogBase64 = Convert.ToBase64String(logBytes),
                diagnosticLogTruncated = truncated
            };
        }

        private static byte[] ReadDiagnosticLogBytes(int maxBytes, out bool truncated)
        {
            truncated = false;
            var path = DebugManager.GetDiagLogPath();
            if (!File.Exists(path))
            {
                return Array.Empty<byte>();
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length <= maxBytes)
            {
                return bytes;
            }

            truncated = true;
            var tail = new byte[maxBytes];
            Buffer.BlockCopy(bytes, bytes.Length - maxBytes, tail, 0, maxBytes);
            return tail;
        }

        private static IEnumerator SendHealthProbeCoroutine(DeviceLogUploadSettings settings, string reason)
        {
            var healthUrl = GetHealthUrl(settings.endpointUrl);
            if (string.IsNullOrWhiteSpace(healthUrl))
            {
                yield break;
            }

            using var request = UnityWebRequest.Get(healthUrl);
            ApplyCommonHeaders(settings, request);

            yield return SendWithTimeoutCoroutine(request, settings.UploadTimeoutSeconds);

            DebugManager.DiagStability(
                $"[DEVICE LOG] health probe reason={reason} result={request.result} " +
                $"responseCode={request.responseCode} error={request.error}");
        }

        private static IEnumerator SendPayloadCoroutine(DeviceLogUploadSettings settings, string json, string reason)
        {
            using var request = new UnityWebRequest(settings.endpointUrl, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            ApplyCommonHeaders(settings, request);

            yield return SendWithTimeoutCoroutine(request, settings.UploadTimeoutSeconds);

            if (request.result != UnityWebRequest.Result.Success)
            {
                DebugManager.DiagStability(
                    $"[DEVICE LOG] upload failed reason={reason} result={request.result} " +
                    $"responseCode={request.responseCode} error={request.error}");
                yield break;
            }

            DebugManager.DiagStability(
                $"[DEVICE LOG] upload completed reason={reason} responseCode={request.responseCode}");
        }

        private static IEnumerator SendWithTimeoutCoroutine(UnityWebRequest request, int timeoutSeconds)
        {
            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability(
                    $"[DEVICE LOG] request start failed url={request.url} " +
                    $"exception={exception.GetType().Name}: {exception.Message}");
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    request.Abort();
                    break;
                }

                yield return null;
            }
        }

        private static string GetHealthUrl(string endpointUrl)
        {
            if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpoint))
            {
                return null;
            }

            var builder = new UriBuilder(endpoint)
            {
                Path = "health",
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri.ToString();
        }

        private static void ApplyCommonHeaders(DeviceLogUploadSettings settings, UnityWebRequest request)
        {
            if (!string.IsNullOrWhiteSpace(settings.sharedToken))
            {
                request.SetRequestHeader(_tokenHeaderName, settings.sharedToken);
            }

            request.SetRequestHeader(_ngrokSkipBrowserWarningHeaderName, "true");
        }

        [Serializable]
        private sealed class DeviceLogUploadPayload
        {
            public DeviceLogUploadMetadata metadata;
            public string diagnosticLogFileName;
            public string diagnosticLogEncoding;
            public string diagnosticLogBase64;
            public bool diagnosticLogTruncated;
        }

        [Serializable]
        private sealed class DeviceLogUploadMetadata
        {
            public string sessionId;
            public string reason;
            public string createdAtUtc;
            public string sessionStartedAtUtc;
            public string platform;
            public string unityVersion;
            public string appVersion;
            public string deviceModel;
            public string operatingSystem;
            public string internetReachability;
            public string buildLabel;
            public string branch;
            public string shortSha;
            public bool dirty;
            public string endpointUrl;
            public string activeScene;
            public string currentLevel;
        }
    }
}
