#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Editor-скрипт для запуска бота на конкретном уровне.
    /// <para>Два режима работы:</para>
    /// <list type="number">
    /// <item>Ручной: через меню Tools → Bot Training → Run Level ...</item>
    /// <item>Автоматический (file-watcher): внешний процесс пишет в
    ///   <c>EditorLogs/bot_training_command.txt</c> адрес уровня.
    ///   Watcher подхватывает файл, запускает Play Mode.
    ///   По окончании бот пишет результат в <c>EditorLogs/bot_training_result.txt</c>
    ///   и Play Mode останавливается. Watcher пишет <c>bot_training_ready.txt</c>
    ///   когда готов принять следующую команду.</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class BotTrainingRunner
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string OverridePrefsKey = "TestLevel_Address";
        private const string StopOnFinishKey = "BotTraining_StopOnFinish";

        private static readonly string EditorLogsDir =
            Path.Combine(Application.dataPath, "..", "EditorLogs");

        private static string CommandFilePath =>
            Path.Combine(EditorLogsDir, "bot_training_command.txt");

        private static string ResultFilePath =>
            Path.Combine(EditorLogsDir, "bot_training_result.txt");

        private static string ReadyFilePath =>
            Path.Combine(EditorLogsDir, "bot_training_ready.txt");

        // ──────────────── File-watcher init ────────────────

        static BotTrainingRunner()
        {
            EditorApplication.update += PollForCommand;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Editor: не паузить игру при потере фокуса (чтобы бот продолжал играть)
            Application.runInBackground = true;
            WriteReadySignal();
        }

        // ──────────────── Menu Items ────────────────

        [MenuItem("Tools/Bot Training/Run Level 01 (Morning)", priority = 60)]
        public static void RunLevel01()
        {
            LaunchLevel("01_New_York/Morning/level_01");
        }

        [MenuItem("Tools/Bot Training/Run Level 02 (Morning)", priority = 61)]
        public static void RunLevel02()
        {
            LaunchLevel("01_New_York/Morning/level_02");
        }

        [MenuItem("Tools/Bot Training/Run Test Medium NotAlive", priority = 62)]
        public static void RunTestMediumNotAlive()
        {
            LaunchLevel("01_New_York/Morning/test_medium_notalive");
        }

        // ──────────────── File-watcher polling ────────────────

        private static void PollForCommand()
        {
            // Не запускать во время Play Mode или компиляции
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;

            if (!File.Exists(CommandFilePath))
                return;

            try
            {
                string levelAddress = File.ReadAllText(CommandFilePath).Trim();
                File.Delete(CommandFilePath);

                if (!string.IsNullOrEmpty(levelAddress))
                {
                    // Удаляем ready-сигнал — мы заняты
                    DeleteReadySignal();
                    Debug.Log($"[BotTrainingRunner] File-watcher: received command → {levelAddress}");
                    LaunchLevel(levelAddress);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BotTrainingRunner] Error reading command file: {ex.Message}");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Когда полностью вышли из Play Mode — сигнализируем готовность
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                WriteReadySignal();
            }
        }

        private static void WriteReadySignal()
        {
            try
            {
                Directory.CreateDirectory(EditorLogsDir);
                File.WriteAllText(ReadyFilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch { /* ignore */ }
        }

        private static void DeleteReadySignal()
        {
            try { if (File.Exists(ReadyFilePath)) File.Delete(ReadyFilePath); }
            catch { /* ignore */ }
        }

        // ──────────────── Core ────────────────

        /// <summary>
        /// Запускает уровень: ставит override в PlayerPrefs, открывает Bootstrap, входит в Play Mode.
        /// По окончании уровня HamsterBot остановит Play Mode и запишет результат.
        /// </summary>
        public static void LaunchLevel(string levelAddress)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[BotTrainingRunner] Already in Play Mode. Exit first.");
                return;
            }

            PlayerPrefs.SetString(OverridePrefsKey, levelAddress);
            PlayerPrefs.SetInt(StopOnFinishKey, 1);
            PlayerPrefs.Save();

            Debug.Log($"[BotTrainingRunner] Launching level: {levelAddress}");

            // Очищаем предыдущий результат
            try { if (File.Exists(ResultFilePath)) File.Delete(ResultFilePath); }
            catch { /* ignore */ }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Проверяет, установлен ли флаг "остановить Play Mode по окончании".
        /// Вызывается из HamsterBot.OnGameFinished().
        /// </summary>
        public static bool ShouldStopOnFinish()
        {
            return PlayerPrefs.GetInt(StopOnFinishKey, 0) == 1;
        }

        /// <summary>
        /// Записывает результат сессии в файл и останавливает Play Mode.
        /// Вызывается из HamsterBot.OnGameFinished().
        /// </summary>
        public static void WriteResultAndStop(string result)
        {
            try
            {
                Directory.CreateDirectory(EditorLogsDir);
                File.WriteAllText(ResultFilePath, result);
                Debug.Log($"[BotTrainingRunner] Result written to EditorLogs/bot_training_result.txt");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BotTrainingRunner] Failed to write result: {ex.Message}");
            }

            // Очищаем флаг
            PlayerPrefs.DeleteKey(StopOnFinishKey);
            PlayerPrefs.Save();

            // Останавливаем Play Mode через delayCall чтобы не крашнуть текущий кадр
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = false;
                Debug.Log("[BotTrainingRunner] Play Mode stopped.");
            };
        }
    }
}
#endif
