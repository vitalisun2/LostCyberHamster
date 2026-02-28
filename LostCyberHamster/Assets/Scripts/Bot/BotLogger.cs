using System;
using System.IO;
using System.Text;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Файловый логгер бота. Пишет текстовый лог сессии в EditorLogs/bot_sessions/.
    /// Формат: timestamp | event | data
    /// </summary>
    public class BotLogger : IDisposable
    {
        private const string SessionFolder = "bot_sessions";
        private StreamWriter _writer;
        private string _sessionFilePath;
        private float _sessionStartTime;
        private int _eventCount;
        private bool _disposed;

        public BotLogger()
        {
            // Не открываем файл сразу — ждём OnBotEnabled
        }

        /// <summary>
        /// Начинает новую сессию логирования.
        /// </summary>
        public void OnBotEnabled(BotMode mode)
        {
            CloseWriter();

            string baseDir;
#if UNITY_EDITOR
            baseDir = Path.Combine(Application.dataPath, "..", "EditorLogs", SessionFolder);
#else
            baseDir = Path.Combine(Application.persistentDataPath, SessionFolder);
#endif
            Directory.CreateDirectory(baseDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _sessionFilePath = Path.Combine(baseDir, $"bot_{mode}_{timestamp}.log");
            _writer = new StreamWriter(_sessionFilePath, append: false, Encoding.UTF8) { AutoFlush = true };
            _sessionStartTime = Time.time;
            _eventCount = 0;

            WriteHeader(mode);
        }

        /// <summary>
        /// Завершает сессию.
        /// </summary>
        public void OnBotDisabled(int framesAlive, int actionsExecuted)
        {
            if (_writer == null) return;

            float duration = Time.time - _sessionStartTime;
            _writer.WriteLine($"--- SESSION END | duration={duration:F1}s | frames={framesAlive} " +
                              $"| actions={actionsExecuted} | events={_eventCount} ---");
            CloseWriter();
        }

        /// <summary>
        /// Логирует действие бота.
        /// </summary>
        public void LogAction(BotDecision decision, Hamster hamster)
        {
            if (_writer == null) return;

            float t = Time.time - _sessionStartTime;
            var state = hamster.HamsterState.Value;
            int energy = hamster.Energy.Value;
            int lives = hamster.Lives.Value;
            bool bottom = hamster.IsOnBottomLine.Value;
            int ulta = hamster.UltaChargeAmount.Value;

            _writer.WriteLine(
                $"{t:F3} | ACTION | {decision.Action} | {decision.Reason} " +
                $"| conf={decision.Confidence:F2} | planned={decision.IsPlanned} " +
                $"| state={state} | energy={energy} | lives={lives} | lane={(bottom ? "bottom" : "top")} " +
                $"| ulta={ulta}");
            _eventCount++;
        }

        /// <summary>
        /// Логирует игровые события (столкновения, сбор, и т.д.).
        /// </summary>
        public void LogEvent(string eventType, string data)
        {
            if (_writer == null) return;

            float t = Time.time - _sessionStartTime;
            _writer.WriteLine($"{t:F3} | EVENT  | {eventType} | {data}");
            _eventCount++;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseWriter();
        }

        // ──────────────── Private ────────────────

        private void WriteHeader(BotMode mode)
        {
            _writer.WriteLine($"=== HamsterBot Session ===");
            _writer.WriteLine($"Mode: {mode}");
            _writer.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine($"Unity Version: {Application.unityVersion}");
            _writer.WriteLine("---");
        }

        private void CloseWriter()
        {
            _writer?.Flush();
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
