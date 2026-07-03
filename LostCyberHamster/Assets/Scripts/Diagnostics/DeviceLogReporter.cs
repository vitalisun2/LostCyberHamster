using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Diagnostics
{
    public sealed class DeviceLogReporter : MonoBehaviour
    {
        private const float _errorUploadCooldownSeconds = 3f;

        private static DeviceLogReporter _instance;

        private float _nextErrorUploadAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(DeviceLogReporter));
            _instance = host.AddComponent<DeviceLogReporter>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
            Application.logMessageReceivedThreaded -= OnLogMessageReceivedThreaded;
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            DebugManager.DiagStability("[DEVICE LOG] reporter awake");
            DeviceLogUploader.UploadDiagnosticLog("session_started_awake");
            StartCoroutine(new DeviceLogStartupProbe().Run());
            StartCoroutine(UploadStartupCheckpoints());
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                DeviceLogUploader.UploadDiagnosticLog("application_paused");
            }
        }

        private void OnApplicationQuit()
        {
            DeviceLogUploader.UploadDiagnosticLog("application_quit");
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded -= OnLogMessageReceivedThreaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private IEnumerator UploadStartupCheckpoints()
        {
            yield return null;
            DeviceLogUploader.UploadDiagnosticLog("startup_after_first_frame");

            yield return new WaitForSecondsRealtime(2f);
            DeviceLogUploader.UploadDiagnosticLog("startup_after_2s");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DebugManager.DiagStability($"[DEVICE LOG] scene loaded name={scene.name} mode={mode}");
            DeviceLogUploader.UploadDiagnosticLog($"scene_loaded_{scene.name}");
            StartCoroutine(UploadSceneLoadedCheckpoints(scene.name));
        }

        private IEnumerator UploadSceneLoadedCheckpoints(string sceneName)
        {
            yield return null;
            DebugManager.DiagStability($"[DEVICE LOG] scene first frame name={sceneName}");
            DeviceLogUploader.UploadDiagnosticLog($"scene_first_frame_{sceneName}");

            yield return new WaitForSecondsRealtime(1f);
            DebugManager.DiagStability($"[DEVICE LOG] scene after 1s name={sceneName}");
            DeviceLogUploader.UploadDiagnosticLog($"scene_after_1s_{sceneName}");
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            {
                return;
            }

            DebugManager.DiagStability(
                $"[DEVICE LOG] captured type={type} condition={condition} stack={stackTrace}");

            if (Time.unscaledTime < _nextErrorUploadAt)
            {
                return;
            }

            _nextErrorUploadAt = Time.unscaledTime + _errorUploadCooldownSeconds;
            DeviceLogUploader.UploadDiagnosticLog(type == LogType.Exception ? "runtime_exception" : "runtime_error");
        }

        private void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            {
                return;
            }

            DebugManager.DiagStability(
                $"[DEVICE LOG] captured threaded type={type} condition={condition} stack={stackTrace}");
        }
    }
}
