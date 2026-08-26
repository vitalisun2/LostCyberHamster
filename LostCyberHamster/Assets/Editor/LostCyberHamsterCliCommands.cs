#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Открывает узкие проектные операции LostCyberHamster для Unity CLI и MCP.
    /// </summary>
    internal static class LostCyberHamsterCliCommands
    {
        private const string StabilityChannel = "[CH=STAB]";
        private const string BotChannel = "[CH=BOT]";
        private const string EconomyChannel = "[CH=ECO]";
        private const string TestResultMarker = "[TEST RESULT]";

        /// <summary>
        /// Возвращает состояние Editor и проектной automation.
        /// </summary>
        [CliCommand(
            "lch_editor_status",
            "Состояние LostCyberHamster Editor, активной сцены и test-level automation.",
            Tags = new[] { "lch", "lch/editor" })]
        public static EditorStatusResult GetEditorStatus()
        {
            var activeScene = SceneManager.GetActiveScene();
            return new EditorStatusResult
            {
                Status = GetEditorState(),
                UnityVersion = Application.unityVersion,
                ProjectPath = Path.GetDirectoryName(Application.dataPath),
                IsCompiling = EditorApplication.isCompiling,
                IsUpdating = EditorApplication.isUpdating,
                PlayMode = GetPlayModeState(),
                ActiveScenePath = activeScene.path,
                ActiveSceneDirty = activeScene.isDirty,
                Automation = TestLevelAutomationBridge.GetAutomationStatus()
            };
        }

        /// <summary>
        /// Пересоздаёт generated project files через текущий Unity generator.
        /// </summary>
        [CliCommand(
            "lch_project_regenerate_files",
            "Пересоздать generated .csproj и .sln проекта.",
            Tags = new[] { "lch", "lch/project" })]
        public static OperationResult RegenerateProjectFiles()
        {
            if (!TestLevelAutomationBridge.TryRegenerateProjectFiles(out var message))
            {
                throw new InvalidOperationException(message);
            }

            return new OperationResult
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// Ставит test level в общую очередь automation.
        /// </summary>
        [CliCommand(
            "lch_test_level_launch",
            "Поставить test level в очередь запуска. Результат читать через lch_test_level_status.",
            Tags = new[] { "lch", "lch/test-level" })]
        public static TestLevelAutomationBridge.BridgeResponse LaunchTestLevel(
            [CliArg("level_address", "Addressables-адрес test level.", Required = true)] string levelAddress,
            [CliArg("time_scale", "Time.timeScale override; 0 использует значение проекта.")] float timeScale = 0f)
        {
            var response = TestLevelAutomationBridge.QueueTestLevelLaunch(levelAddress, timeScale);
            if (response.state == "failed" || response.state == "busy")
            {
                throw new InvalidOperationException(response.message);
            }

            return response;
        }

        /// <summary>
        /// Возвращает состояние последнего test-level запроса.
        /// </summary>
        [CliCommand(
            "lch_test_level_status",
            "Состояние и результат последнего test-level automation запроса.",
            Tags = new[] { "lch", "lch/test-level" })]
        public static TestLevelAutomationBridge.BridgeResponse GetTestLevelStatus()
        {
            return TestLevelAutomationBridge.GetTestLevelStatus();
        }

        /// <summary>
        /// Возвращает краткий итог diagnostic log по каналам STAB, BOT и ECO.
        /// </summary>
        [CliCommand(
            "lch_diagnostics_summary",
            "Краткий итог diagnostic log по каналам STAB, BOT и ECO.",
            Tags = new[] { "lch", "lch/diagnostics" })]
        public static DiagnosticsSummary GetDiagnosticsSummary()
        {
            var logPath = DebugManager.GetDiagLogPath();
            if (!File.Exists(logPath))
            {
                return new DiagnosticsSummary
                {
                    LogPath = logPath,
                    Exists = false,
                    Stability = new DiagnosticChannelSummary(),
                    Bot = new DiagnosticChannelSummary(),
                    Economy = new DiagnosticChannelSummary()
                };
            }

            // Читаем текущий файл с совместным доступом с runtime logger.
            string content;
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                content = reader.ReadToEnd();
            }

            // Собираем счётчики и последние строки каждого канала.
            var result = new DiagnosticsSummary
            {
                LogPath = logPath,
                Exists = true,
                Stability = new DiagnosticChannelSummary(),
                Bot = new DiagnosticChannelSummary(),
                Economy = new DiagnosticChannelSummary()
            };

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            result.TotalLines = lines.Length;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                UpdateChannel(result.Stability, line, StabilityChannel);
                UpdateChannel(result.Bot, line, BotChannel);
                UpdateChannel(result.Economy, line, EconomyChannel);

                if (line.Contains(TestResultMarker))
                {
                    result.LastTestResult = line.Trim();
                }
            }

            return result;
        }

        private static string GetEditorState()
        {
            if (EditorApplication.isCompiling)
            {
                return "compiling";
            }

            if (EditorApplication.isUpdating)
            {
                return "updating";
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "playing";
            }

            return "ready";
        }

        private static string GetPlayModeState()
        {
            if (EditorApplication.isPaused)
            {
                return "paused";
            }

            if (EditorApplication.isPlaying)
            {
                return "playing";
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "transitioning";
            }

            return "stopped";
        }

        private static void UpdateChannel(DiagnosticChannelSummary summary, string line, string marker)
        {
            if (!line.Contains(marker))
            {
                return;
            }

            summary.Count++;
            summary.LastLine = line.Trim();
        }

        [Serializable]
        public sealed class EditorStatusResult
        {
            public string Status { get; set; }
            public string UnityVersion { get; set; }
            public string ProjectPath { get; set; }
            public bool IsCompiling { get; set; }
            public bool IsUpdating { get; set; }
            public string PlayMode { get; set; }
            public string ActiveScenePath { get; set; }
            public bool ActiveSceneDirty { get; set; }
            public TestLevelAutomationBridge.BridgeResponse Automation { get; set; }
        }

        [Serializable]
        public sealed class OperationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        [Serializable]
        public sealed class DiagnosticsSummary
        {
            public string LogPath { get; set; }
            public bool Exists { get; set; }
            public int TotalLines { get; set; }
            public DiagnosticChannelSummary Stability { get; set; }
            public DiagnosticChannelSummary Bot { get; set; }
            public DiagnosticChannelSummary Economy { get; set; }
            public string LastTestResult { get; set; }
        }

        [Serializable]
        public sealed class DiagnosticChannelSummary
        {
            public int Count { get; set; }
            public string LastLine { get; set; }
        }
    }
}
#endif
