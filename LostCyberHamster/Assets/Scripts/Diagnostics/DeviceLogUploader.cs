using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Online;
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
            if (!IsUploadEnabled())
            {
                return;
            }

            DeviceLogUploadRunner.Enqueue(reason);
        }

        public static bool IsUploadEnabled()
        {
            return IsUploadEnabled(logDisabled: false);
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

        internal static bool IsCurrentEndpoint(string endpoint)
        {
            return string.Equals(LoadSettings()?.endpointUrl, endpoint, StringComparison.Ordinal);
        }

        internal static bool TryPrepareUpload(string reason, out DeviceLogUploadSettings settings, out string json)
        {
            settings = null;
            json = null;

            try
            {
                settings = LoadSettings();
                if (!ShouldUpload(settings, logDisabled: false))
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

        private static bool IsUploadEnabled(bool logDisabled)
        {
            return ShouldUpload(LoadSettings(), logDisabled);
        }

        private static bool ShouldUpload(DeviceLogUploadSettings settings, bool logDisabled)
        {
            bool shouldUpload = settings != null
                && settings.enabled
                && settings.HasEndpoint
                && settings.IsPlatformAllowed();

            if (!shouldUpload && logDisabled)
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
            // Base64 и metadata также входят в лимит сохранённого снимка 1 МиБ.
            int maxBytes = Math.Min(settings.MaxLogBytes, (1024 * 1024 - 16 * 1024) * 3 / 4);
            var logBytes = ReadDiagnosticLogBytes(maxBytes, out bool truncated);
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

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int count = (int)Math.Min(stream.Length, maxBytes);
            truncated = stream.Length > count;
            stream.Seek(-count, SeekOrigin.End);
            var tail = new byte[count];
            int offset = 0;
            while (offset < tail.Length)
            {
                int read = stream.Read(tail, offset, tail.Length - offset);
                if (read == 0) break;
                offset += read;
            }
            if (offset != tail.Length) Array.Resize(ref tail, offset);
            return tail;
        }

        /// <summary>Отправляет сохранённый снимок; очередь удаляет его только после подтверждённого ответа.</summary>
        internal static async Task UploadPreparedAsync(string json, string endpoint)
        {
            var settings = LoadSettings();
            if (!ShouldUpload(settings, false)) throw new InvalidOperationException("Log upload is disabled.");
            if (!string.Equals(endpoint, settings.endpointUrl, StringComparison.Ordinal))
                throw new InvalidOperationException("Log destination changed.");

            // Один HTTP-запрос с общим realtime deadline не задерживает интерфейс.
            using var request = new UnityWebRequest(settings.endpointUrl, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            ApplyCommonHeaders(settings, request);
            request.timeout = settings.UploadTimeoutSeconds;
            var operation = request.SendWebRequest();
            double deadline = UnityGameClock.Instance.RealtimeSeconds + settings.UploadTimeoutSeconds;
            while (!operation.isDone)
            {
                if (UnityGameClock.Instance.RealtimeSeconds >= deadline)
                {
                    request.Abort();
                    throw new TimeoutException("Log upload timed out.");
                }
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException($"Log upload response: {request.responseCode}.");
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
