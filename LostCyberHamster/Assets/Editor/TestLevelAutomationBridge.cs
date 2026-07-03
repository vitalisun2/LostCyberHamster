#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// File-based bridge for controlling TestLevelLauncher from an already-open Unity Editor.
    /// A terminal script writes a request JSON file, the editor picks it up, runs the launch,
    /// watches diagnostic_log.txt for [TEST RESULT], and writes a response JSON file back.
    /// </summary>
    [InitializeOnLoad]
    internal static class TestLevelAutomationBridge
    {
        private const string LaunchCommand = "launch_test_level";
        private const string RecompileCommand = "recompile_scripts";
        private const string RegenerateProjectFilesCommand = "regenerate_project_files";
        private const string ProbeTutorialStopAfterStepCommand = "probe_tutorial_stop_after_step";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string FirstGameplayLevelAddress = "01_New_York/Morning/level_01";
        private const string TestLevelPrefsKey = "TestLevel_Address";
        private const string SkipIntroPrefsKey = "TestLevel_SkipIntro";
        private const string TutorialStopAfterStepPrefsKey = "Tutorial_StopAfterStep";
        private const string RequestIdSessionKey = "TestLevelAutomationBridge.RequestId";
        private const string CommandSessionKey = "TestLevelAutomationBridge.Command";
        private const string ResultSessionKey = "TestLevelAutomationBridge.Result";
        private const string ResultLineSessionKey = "TestLevelAutomationBridge.ResultLine";
        private const double PollIntervalSeconds = 0.25d;

        private static readonly string AutomationDirectoryPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "../EditorLogs/automation"));

        private static readonly string RequestFilePath = Path.Combine(AutomationDirectoryPath, "test_level_request.json");
        private static readonly string ResponseFilePath = Path.Combine(AutomationDirectoryPath, "test_level_response.json");

        private static double _nextPollAt;

        private static void RefreshAssetDatabase()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        [Serializable]
        private sealed class BridgeRequest
        {
            public string requestId;
            public string command;
            public string createdAtUtc;
            public string levelAddress;
            public float timeScale;
            public int stopAfterStep;
        }

        [Serializable]
        private sealed class BridgeResponse
        {
            public string requestId;
            public string command;
            public string state;
            public string testResult;
            public string message;
            public string updatedAtUtc;
            public string diagnosticLogPath;
        }

        static TestLevelAutomationBridge()
        {
            Directory.CreateDirectory(AutomationDirectoryPath);

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextPollAt)
            {
                return;
            }

            _nextPollAt = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            TryProcessIncomingRequest();
            MonitorActiveRun();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !HasActiveRequest())
            {
                return;
            }

            var requestId = GetActiveRequestId();
            var command = GetActiveCommand();
            var result = GetStoredResult();
            var resultLine = GetStoredResultLine();

            if (string.IsNullOrEmpty(result) && TryReadTestResult(out var detectedResult, out var detectedLine))
            {
                result = detectedResult;
                resultLine = detectedLine;
            }

            if (string.IsNullOrEmpty(result))
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = requestId,
                    command = command,
                    state = "failed",
                    testResult = string.Empty,
                    message = "Play mode ended before a [TEST RESULT] marker was detected.",
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });
            }
            else
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = requestId,
                    command = command,
                    state = "completed",
                    testResult = result,
                    message = resultLine,
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });
            }

            ClearActiveRequest();
        }

        private static void TryProcessIncomingRequest()
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            // Force Unity to synchronously import git-added scripts before we inspect the request.
            RefreshAssetDatabase();

            if (!TryReadRequest(out var request, out var errorMessage))
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = string.Empty,
                    command = string.Empty,
                    state = "failed",
                    testResult = string.Empty,
                    message = errorMessage,
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });
                SafeDelete(RequestFilePath);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.requestId))
            {
                request.requestId = Guid.NewGuid().ToString("N");
            }

            if (!string.Equals(request.command, LaunchCommand, StringComparison.Ordinal) &&
                !string.Equals(request.command, RecompileCommand, StringComparison.Ordinal) &&
                !string.Equals(request.command, RegenerateProjectFilesCommand, StringComparison.Ordinal) &&
                !string.Equals(request.command, ProbeTutorialStopAfterStepCommand, StringComparison.Ordinal))
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = request.requestId,
                    command = request.command,
                    state = "failed",
                    testResult = string.Empty,
                    message = $"Unsupported command: {request.command}",
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });
                SafeDelete(RequestFilePath);
                return;
            }

            if (HasActiveRequest() || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = request.requestId,
                    command = request.command,
                    state = "busy",
                    testResult = string.Empty,
                    message = "Unity Editor is already processing another test-level automation request.",
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });
                SafeDelete(RequestFilePath);
                return;
            }

            SetActiveRequestId(request.requestId);
            SetActiveCommand(request.command);
            ClearStoredResult();

            SafeDelete(RequestFilePath);

            if (string.Equals(request.command, RecompileCommand, StringComparison.Ordinal))
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = request.requestId,
                    command = request.command,
                    state = "running",
                    testResult = string.Empty,
                    message = "Request accepted. Forcing Unity script recompilation.",
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });

                RefreshAssetDatabase();
                CompilationPipeline.RequestScriptCompilation();
                return;
            }

            if (string.Equals(request.command, RegenerateProjectFilesCommand, StringComparison.Ordinal))
            {
                if (!TryRegenerateProjectFiles(out var regenerateMessage))
                {
                    WriteResponse(new BridgeResponse
                    {
                        requestId = request.requestId,
                        command = request.command,
                        state = "failed",
                        testResult = string.Empty,
                        message = regenerateMessage,
                        updatedAtUtc = DateTime.UtcNow.ToString("O"),
                        diagnosticLogPath = DebugManager.GetDiagLogPath()
                    });

                    ClearActiveRequest();
                    return;
                }

                WriteResponse(new BridgeResponse
                {
                    requestId = request.requestId,
                    command = request.command,
                    state = "completed",
                    testResult = string.Empty,
                    message = regenerateMessage,
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });

                ClearActiveRequest();
                return;
            }

            if (string.Equals(request.command, ProbeTutorialStopAfterStepCommand, StringComparison.Ordinal))
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = request.requestId,
                    command = request.command,
                    state = "running",
                    testResult = string.Empty,
                    message = $"Request accepted. Probing tutorial stop after step {request.stopAfterStep}.",
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });

                DebugManager.ClearDiagLog();
                LaunchTutorialStopAfterStepProbe(Mathf.Max(1, request.stopAfterStep));
                return;
            }

            WriteResponse(new BridgeResponse
            {
                requestId = request.requestId,
                command = request.command,
                state = "running",
                testResult = string.Empty,
                message = "Request accepted. Launching test level.",
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                diagnosticLogPath = DebugManager.GetDiagLogPath()
            });

            DebugManager.ClearDiagLog();

            if (!TestLevelLauncher.TryLaunchTestLevelAutomation(request.levelAddress, request.timeScale, out var launchError))
            {
                WriteResponse(new BridgeResponse
                {
                    requestId = request.requestId,
                    command = request.command,
                    state = "failed",
                    testResult = string.Empty,
                    message = launchError,
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    diagnosticLogPath = DebugManager.GetDiagLogPath()
                });
                ClearActiveRequest();
                return;
            }
        }

        private static void LaunchTutorialStopAfterStepProbe(int stopAfterStep)
        {
            PlayerPrefs.SetString(TestLevelPrefsKey, FirstGameplayLevelAddress);
            PlayerPrefs.SetInt(SkipIntroPrefsKey, 1);
            PlayerPrefs.SetInt(Assets.Scripts.Tutorial.TutorialAutomationSettings.AutoPlayKey, 1);
            PlayerPrefs.SetInt(Assets.Scripts.Tutorial.TutorialLaunchState.ResetCompletedOnceKey, 1);
            PlayerPrefs.SetInt(TutorialStopAfterStepPrefsKey, stopAfterStep);

            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void MonitorActiveRun()
        {
            if (!HasActiveRequest())
            {
                return;
            }

            var activeCommand = GetActiveCommand();
            if (string.Equals(activeCommand, RecompileCommand, StringComparison.Ordinal))
            {
                MonitorCompilationRequest();
                return;
            }

            // regenerate_project_files completes synchronously in TryProcessIncomingRequest,
            // so no monitoring is needed here.

            if (!string.IsNullOrEmpty(GetStoredResult()))
            {
                return;
            }

            if (!TryReadTestResult(out var result, out var resultLine))
            {
                return;
            }

            SetStoredResult(result, resultLine);

            WriteResponse(new BridgeResponse
            {
                requestId = GetActiveRequestId(),
                command = activeCommand,
                state = "stopping",
                testResult = result,
                message = resultLine,
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                diagnosticLogPath = DebugManager.GetDiagLogPath()
            });

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void MonitorCompilationRequest()
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            WriteResponse(new BridgeResponse
            {
                requestId = GetActiveRequestId(),
                command = GetActiveCommand(),
                state = "completed",
                testResult = string.Empty,
                message = "Unity script compilation completed.",
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                diagnosticLogPath = DebugManager.GetDiagLogPath()
            });

            ClearActiveRequest();
        }

        private static bool TryReadRequest(out BridgeRequest request, out string errorMessage)
        {
            request = null;
            errorMessage = null;

            try
            {
                var json = File.ReadAllText(RequestFilePath);
                request = JsonUtility.FromJson<BridgeRequest>(json);
                if (request == null)
                {
                    errorMessage = "Automation request JSON is empty or invalid.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Failed to read automation request: {exception.Message}";
                return false;
            }
        }

        private static bool TryRegenerateProjectFiles(out string message)
        {
            message = null;

            var installationType = Type.GetType("Microsoft.Unity.VisualStudio.Editor.VisualStudioForWindowsInstallation, Unity.VisualStudio.Editor");
            if (installationType == null)
            {
                message = "Unable to regenerate project files: VisualStudioForWindowsInstallation type was not found.";
                return false;
            }

            var generatorField = installationType.GetField("_generator", BindingFlags.NonPublic | BindingFlags.Static);
            var projectGenerator = generatorField?.GetValue(null);
            if (projectGenerator == null)
            {
                message = "Unable to regenerate project files: Visual Studio package generator instance was not found.";
                return false;
            }

            var syncMethod = projectGenerator.GetType().GetMethod("Sync", BindingFlags.Public | BindingFlags.Instance);
            if (syncMethod == null)
            {
                message = $"Unable to regenerate project files: Sync() was not found on generator type '{projectGenerator.GetType().FullName}'.";
                return false;
            }

            try
            {
                syncMethod.Invoke(projectGenerator, null);
                message = $"Unity project files regenerated through {projectGenerator.GetType().Name}.Sync().";
                return true;
            }
            catch (TargetInvocationException exception)
            {
                var inner = exception.InnerException;
                var detail = inner == null
                    ? exception.ToString()
                    : $"{inner.GetType().FullName}: {inner.Message}{Environment.NewLine}{inner.StackTrace}";
                message = $"Failed to regenerate project files: {detail}";
                return false;
            }
            catch (Exception exception)
            {
                message = $"Failed to regenerate project files: {exception}";
                return false;
            }
        }

        private static bool TryReadTestResult(out string result, out string resultLine)
        {
            result = null;
            resultLine = null;

            var logPath = DebugManager.GetDiagLogPath();
            if (!File.Exists(logPath))
            {
                return false;
            }

            try
            {
                var lines = ReadAllLinesShared(logPath);
                for (var index = lines.Length - 1; index >= 0; index--)
                {
                    var line = lines[index];
                    if (!line.Contains("[TEST RESULT]"))
                    {
                        continue;
                    }

                    resultLine = line.Trim();
                    if (line.Contains("WIN"))
                    {
                        result = "WIN";
                        return true;
                    }

                    if (line.Contains("FAIL"))
                    {
                        result = "FAIL";
                        return true;
                    }

                    result = "UNKNOWN";
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TestLevelAutomationBridge] Failed to inspect diagnostic log: {exception.Message}");
            }

            return false;
        }

        private static string[] ReadAllLinesShared(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        private static void WriteResponse(BridgeResponse response)
        {
            Directory.CreateDirectory(AutomationDirectoryPath);

            var tempPath = ResponseFilePath + ".tmp";
            var json = JsonUtility.ToJson(response, true);

            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, ResponseFilePath, overwrite: true);
            SafeDelete(tempPath);
        }

        private static bool HasActiveRequest()
        {
            return !string.IsNullOrEmpty(GetActiveRequestId());
        }

        private static string GetActiveRequestId()
        {
            return SessionState.GetString(RequestIdSessionKey, string.Empty);
        }

        private static void SetActiveRequestId(string requestId)
        {
            SessionState.SetString(RequestIdSessionKey, requestId ?? string.Empty);
        }

        private static string GetActiveCommand()
        {
            return SessionState.GetString(CommandSessionKey, string.Empty);
        }

        private static void SetActiveCommand(string command)
        {
            SessionState.SetString(CommandSessionKey, command ?? string.Empty);
        }

        private static string GetStoredResult()
        {
            return SessionState.GetString(ResultSessionKey, string.Empty);
        }

        private static string GetStoredResultLine()
        {
            return SessionState.GetString(ResultLineSessionKey, string.Empty);
        }

        private static void SetStoredResult(string result, string resultLine)
        {
            SessionState.SetString(ResultSessionKey, result ?? string.Empty);
            SessionState.SetString(ResultLineSessionKey, resultLine ?? string.Empty);
        }

        private static void ClearStoredResult()
        {
            SetStoredResult(string.Empty, string.Empty);
        }

        private static void ClearActiveRequest()
        {
            SetActiveRequestId(string.Empty);
            SetActiveCommand(string.Empty);
            ClearStoredResult();
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TestLevelAutomationBridge] Failed to delete file '{path}': {exception.Message}");
            }
        }
    }
}
#endif
