using System;
using System.Collections.Generic;
using global::System.IO;
using System.Text;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Полный отчёт бот-сессии для режима Analytics.
    /// Собирает статистику, паттерны, экономику, аномалии.
    /// Сериализуется в JSON для дальнейшего анализа.
    /// </summary>
    [Serializable]
    public class BotSessionReport
    {
        // ──────────────── Meta ────────────────
        public string Mode;
        public string StartedAt;
        public float DurationSec;
        public int TotalFrames;

        // ──────────────── Actions ────────────────
        public int TotalActions;
        public Dictionary<string, int> ActionCounts = new Dictionary<string, int>();
        public float ActionsPerSecond;

        // ──────────────── Game Stats ────────────────
        public int TotalCoinsCollected;
        public int TotalLivesLost;
        public int TotalLivesGained;
        public int TotalEnergySpent;
        public int TotalEnergyGained;
        public int TotalCollisions;
        public int TotalJumpsOn;
        public int TotalJumpsOver;
        public int UltaUsedCount;

        // ──────────────── Economy ────────────────
        public float AvgEnergyPerAction;
        public float CoinsPerMinute;
        public float CollisionsPerMinute;

        // ──────────────── Patterns ────────────────
        public float AvgTimeBetweenActions;
        public string MostCommonAction;
        public int DeathCount;
        public float AvgLifespanSec;

        // ──────────────── Anomalies ────────────────
        public List<string> Anomalies = new List<string>();

        // ──────────────── Log Entries ────────────────
        [NonSerialized]
        private readonly List<BotLogEntry> _entries = new List<BotLogEntry>();
        private float _startTime;

        /// <summary>
        /// Начинает запись новой сессии.
        /// </summary>
        public void BeginSession(BotMode mode)
        {
            Mode = mode.ToString();
            StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _startTime = Time.time;
            _entries.Clear();
            ActionCounts.Clear();
            Anomalies.Clear();
            ResetCounters();
        }

        /// <summary>
        /// Записывает структурированную запись лога.
        /// </summary>
        public void RecordEntry(BotLogEntry entry)
        {
            _entries.Add(entry);

            switch (entry.EntryType)
            {
                case BotLogEntryType.Action:
                    TotalActions++;
                    string actionKey = entry.Action.ToString();
                    if (!ActionCounts.ContainsKey(actionKey))
                        ActionCounts[actionKey] = 0;
                    ActionCounts[actionKey]++;
                    break;

                case BotLogEntryType.GameEvent:
                    RecordGameEvent(entry.EventName, entry.Details);
                    break;
            }
        }

        /// <summary>
        /// Финализирует сессию и считает аналитику.
        /// </summary>
        public void EndSession(int totalFrames)
        {
            DurationSec = Time.time - _startTime;
            TotalFrames = totalFrames;

            // Расчёт метрик
            if (DurationSec > 0)
            {
                ActionsPerSecond = TotalActions / DurationSec;
                CoinsPerMinute = TotalCoinsCollected / (DurationSec / 60f);
                CollisionsPerMinute = TotalCollisions / (DurationSec / 60f);
            }

            if (TotalActions > 0)
            {
                AvgEnergyPerAction = (float)TotalEnergySpent / TotalActions;
                AvgTimeBetweenActions = DurationSec / TotalActions;
            }

            // Самое частое действие
            int maxCount = 0;
            MostCommonAction = "None";
            foreach (var kvp in ActionCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    MostCommonAction = kvp.Key;
                }
            }

            // Аномалии
            DetectAnomalies();
        }

        /// <summary>
        /// Сохраняет отчёт в JSON-файл.
        /// </summary>
        public void SaveToFile()
        {
            string baseDir;
#if UNITY_EDITOR
            baseDir = Path.Combine(Application.dataPath, "..", "EditorLogs", "bot_sessions");
#else
            baseDir = Path.Combine(Application.persistentDataPath, "bot_sessions");
#endif
            Directory.CreateDirectory(baseDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(baseDir, $"report_{Mode}_{timestamp}.json");

            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            DebugManager.DiagLog($"[BotSessionReport] Saved: {path}");
            Debug.Log($"[BotSessionReport] Report saved: {path}");
        }

        /// <summary>
        /// Генерирует краткий текстовый саммари для консоли.
        /// </summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Bot Session Report ({Mode}) ===");
            sb.AppendLine($"Duration: {DurationSec:F1}s | Frames: {TotalFrames}");
            sb.AppendLine($"Actions: {TotalActions} ({ActionsPerSecond:F1}/sec)");
            sb.AppendLine($"Most Common: {MostCommonAction}");
            sb.AppendLine($"Coins: {TotalCoinsCollected} ({CoinsPerMinute:F1}/min)");
            sb.AppendLine($"Collisions: {TotalCollisions} ({CollisionsPerMinute:F1}/min)");
            sb.AppendLine($"Lives -={TotalLivesLost} +={TotalLivesGained}");
            sb.AppendLine($"Energy spent={TotalEnergySpent} gained={TotalEnergyGained} avg/action={AvgEnergyPerAction:F1}");
            sb.AppendLine($"Deaths: {DeathCount}");

            if (Anomalies.Count > 0)
            {
                sb.AppendLine("--- Anomalies ---");
                for (int i = 0; i < Anomalies.Count; i++)
                    sb.AppendLine($"  ! {Anomalies[i]}");
            }

            return sb.ToString();
        }

        // ──────────────── Private ────────────────

        private void RecordGameEvent(string eventName, string data)
        {
            switch (eventName)
            {
                case "CoinCollected":
                    if (int.TryParse(data, out int coinVal))
                        TotalCoinsCollected += coinVal;
                    break;
                case "LivesLost":
                    if (int.TryParse(data, out int livesLost))
                        TotalLivesLost += livesLost;
                    break;
                case "LivesAdded":
                    if (int.TryParse(data, out int livesAdded))
                        TotalLivesGained += livesAdded;
                    break;
                case "EnergySpent":
                    if (int.TryParse(data, out int energySpent))
                        TotalEnergySpent += energySpent;
                    break;
                case "EnergyAdded":
                    if (int.TryParse(data, out int energyAdded))
                        TotalEnergyGained += energyAdded;
                    break;
                case "Collision":
                    TotalCollisions++;
                    break;
                case "JumpedOn":
                    TotalJumpsOn++;
                    break;
                case "JumpedOver":
                    TotalJumpsOver++;
                    break;
                case "UltaUsed":
                    UltaUsedCount++;
                    break;
            }
        }

        private void DetectAnomalies()
        {
            if (TotalActions == 0 && DurationSec > 5f)
                Anomalies.Add("Zero actions in session longer than 5 seconds");

            if (TotalCollisions > TotalActions && TotalActions > 0)
                Anomalies.Add($"More collisions ({TotalCollisions}) than actions ({TotalActions})");

            if (CollisionsPerMinute > 30f)
                Anomalies.Add($"Very high collision rate: {CollisionsPerMinute:F1}/min");

            if (TotalLivesLost > 10 && DurationSec < 60f)
                Anomalies.Add($"Rapid life loss: {TotalLivesLost} lives in {DurationSec:F1}s");

            if (ActionsPerSecond > 15f)
                Anomalies.Add($"Unusually high action rate: {ActionsPerSecond:F1}/sec");

            if (DeathCount > 3 && DurationSec < 30f)
                Anomalies.Add($"Multiple deaths ({DeathCount}) in short session ({DurationSec:F1}s)");
        }

        private void ResetCounters()
        {
            TotalActions = 0;
            TotalCoinsCollected = 0;
            TotalLivesLost = 0;
            TotalLivesGained = 0;
            TotalEnergySpent = 0;
            TotalEnergyGained = 0;
            TotalCollisions = 0;
            TotalJumpsOn = 0;
            TotalJumpsOver = 0;
            UltaUsedCount = 0;
            DeathCount = 0;
        }
    }
}
