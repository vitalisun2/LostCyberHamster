using System;
using System.Collections.Generic;
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
        public void OnBotEnabled(BotMode mode, string levelName = "unknown", BotPlayStyle playStyle = BotPlayStyle.Survival)
        {
            CloseWriter();

            string baseDir;
#if UNITY_EDITOR
            baseDir = Path.Combine(Application.dataPath, "..", "EditorLogs", SessionFolder);
#else
            baseDir = Path.Combine(Application.persistentDataPath, SessionFolder);
#endif
            Directory.CreateDirectory(baseDir);

            CleanupOldLogs(baseDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _sessionFilePath = Path.Combine(baseDir, $"bot_{mode}_{playStyle}_{timestamp}.log");
            _writer = new StreamWriter(_sessionFilePath, append: false, Encoding.UTF8) { AutoFlush = true };
            _sessionStartTime = Time.time;
            _eventCount = 0;

            WriteHeader(mode, levelName, playStyle);
        }

        /// <summary>
        /// Удаляет лог-файлы старше сегодняшнего дня.
        /// </summary>
        private static void CleanupOldLogs(string directory)
        {
            try
            {
                var today = DateTime.Today;
                var files = Directory.GetFiles(directory, "*.log");
                int deleted = 0;

                foreach (var file in files)
                {
                    var lastWrite = File.GetLastWriteTime(file);
                    if (lastWrite.Date < today)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }

                if (deleted > 0)
                    DebugManager.DiagLog($"[BotLogger] Cleaned up {deleted} old log file(s).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BotLogger] Failed to cleanup old logs: {ex.Message}");
            }
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
        /// Логирует действие бота с контекстом угроз.
        /// </summary>
        public void LogAction(BotDecision decision, Hamster hamster,
            IReadOnlyList<ThreatInfo> currentLane = null, IReadOnlyList<ThreatInfo> otherLane = null)
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

            // Контекст: что видит бот на текущей и другой линии
            if (currentLane != null && currentLane.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"{t:F3} | SCAN   | curLane({currentLane.Count}): ");
                int max = currentLane.Count > 5 ? 5 : currentLane.Count;
                for (int i = 0; i < max; i++)
                {
                    var th = currentLane[i];
                    sb.Append($"[{th.Type} @{th.DistanceX:F1} t={th.TimeToReach:F2}] ");
                }
                _writer.WriteLine(sb.ToString());
            }

            if (otherLane != null && otherLane.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"{t:F3} | SCAN   | othLane({otherLane.Count}): ");
                int max = otherLane.Count > 5 ? 5 : otherLane.Count;
                for (int i = 0; i < max; i++)
                {
                    var th = otherLane[i];
                    sb.Append($"[{th.Type} @{th.DistanceX:F1} t={th.TimeToReach:F2}] ");
                }
                _writer.WriteLine(sb.ToString());
            }

            _eventCount++;
        }

        /// <summary>
        /// Логирует полный контекст столкновения с препятствием.
        /// </summary>
        public void LogCollisionContext(
            string obstacleInfo, string hamsterInfo, string decisionTrail,
            IReadOnlyList<ThreatInfo> currentLane, IReadOnlyList<ThreatInfo> otherLane,
            string lastDecision)
        {
            if (_writer == null) return;

            float t = Time.time - _sessionStartTime;
            _writer.WriteLine($"{t:F3} | !!COLLISION!! ========================================");
            _writer.WriteLine($"{t:F3} | COLL_OBJ    | {obstacleInfo}");
            _writer.WriteLine($"{t:F3} | COLL_HMSTR  | {hamsterInfo}");
            _writer.WriteLine($"{t:F3} | COLL_DECIDE | lastAction={lastDecision}");
            if (!string.IsNullOrEmpty(decisionTrail))
                _writer.WriteLine($"{t:F3} | COLL_TRAIL  | {decisionTrail}");

            if (currentLane != null && currentLane.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"{t:F3} | COLL_CUR_LN | ");
                for (int i = 0; i < currentLane.Count && i < 8; i++)
                {
                    var th = currentLane[i];
                    sb.Append($"[{th.Type} @{th.DistanceX:F1} t={th.TimeToReach:F2}] ");
                }
                _writer.WriteLine(sb.ToString());
            }

            if (otherLane != null && otherLane.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"{t:F3} | COLL_OTH_LN | ");
                for (int i = 0; i < otherLane.Count && i < 8; i++)
                {
                    var th = otherLane[i];
                    sb.Append($"[{th.Type} @{th.DistanceX:F1} t={th.TimeToReach:F2}] ");
                }
                _writer.WriteLine(sb.ToString());
            }

            _writer.WriteLine($"{t:F3} | !!COLLISION!! ========================================");
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

        private void WriteHeader(BotMode mode, string levelName, BotPlayStyle playStyle = BotPlayStyle.Survival)
        {
            _writer.WriteLine($"=== HamsterBot Session ===");
            _writer.WriteLine($"Mode: {mode}");
            _writer.WriteLine($"PlayStyle: {playStyle}");
            _writer.WriteLine($"Level: {levelName}");
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
