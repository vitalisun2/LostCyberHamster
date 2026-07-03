using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Scripts.Diagnostics
{
    internal sealed class DeviceLogStartupProbe
    {
        private const string _settingsResourcePath = "Diagnostics/device_log_settings";
        private const string _tokenHeaderName = "X-LCH-Device-Log-Token";
        private const string _ngrokSkipBrowserWarningHeaderName = "ngrok-skip-browser-warning";

        public IEnumerator Run()
        {
            var settings = LoadSettings();
            if (settings == null || !settings.enabled || !settings.HasEndpoint || !settings.IsPlatformAllowed())
            {
                DebugManager.DiagStability("[DEVICE LOG] startup probe skipped settings unavailable or disabled");
                yield break;
            }

            yield return SendHealthProbe(settings);
            yield return SendFormProbe(settings);
        }

        private static DeviceLogUploadSettings LoadSettings()
        {
            var asset = Resources.Load<TextAsset>(_settingsResourcePath);
            return asset == null ? null : JsonUtility.FromJson<DeviceLogUploadSettings>(asset.text);
        }

        private static IEnumerator SendHealthProbe(DeviceLogUploadSettings settings)
        {
            var healthUrl = BuildSiblingUrl(settings.endpointUrl, "health");
            using var request = UnityWebRequest.Get(healthUrl);
            ApplyCommonHeaders(settings, request);

            yield return SendWithTimeout(request, settings.UploadTimeoutSeconds);

            DebugManager.DiagStability(
                $"[DEVICE LOG] startup GET health result={request.result} " +
                $"responseCode={request.responseCode} error={request.error}");
        }

        private static IEnumerator SendFormProbe(DeviceLogUploadSettings settings)
        {
            var probeUrl = BuildSiblingUrl(settings.endpointUrl, "probe");
            var form = new WWWForm();
            form.AddField("reason", "startup_form_probe");
            form.AddField("createdAtUtc", DateTime.UtcNow.ToString("O"));
            form.AddField("deviceModel", SystemInfo.deviceModel);
            form.AddField("platform", Application.platform.ToString());
            form.AddField("buildLabel", settings.buildLabel);

            using var request = UnityWebRequest.Post(probeUrl, form);
            ApplyCommonHeaders(settings, request);

            yield return SendWithTimeout(request, settings.UploadTimeoutSeconds);

            DebugManager.DiagStability(
                $"[DEVICE LOG] startup POST probe result={request.result} " +
                $"responseCode={request.responseCode} error={request.error}");
        }

        private static IEnumerator SendWithTimeout(UnityWebRequest request, int timeoutSeconds)
        {
            var operation = request.SendWebRequest();
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

        private static void ApplyCommonHeaders(DeviceLogUploadSettings settings, UnityWebRequest request)
        {
            if (!string.IsNullOrWhiteSpace(settings.sharedToken))
            {
                request.SetRequestHeader(_tokenHeaderName, settings.sharedToken);
            }

            request.SetRequestHeader(_ngrokSkipBrowserWarningHeaderName, "true");
        }

        private static string BuildSiblingUrl(string endpointUrl, string path)
        {
            var builder = new UriBuilder(endpointUrl)
            {
                Path = path,
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri.ToString();
        }
    }
}
