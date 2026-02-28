using System;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Bot.Learning
{
    /// <summary>
    /// Управляет хранением и загрузкой геномов бота (JSON).
    /// Путь: EditorLogs/BotGenomes/ (Editor) или PersistentDataPath/BotGenomes/ (Build).
    /// Формат имени файла: {PlayStyle}_{LevelName}.json
    /// </summary>
    public class GenomeManager
    {
        private const string GenomesFolder = "BotGenomes";
        private const int MaxHistorySize = 100;

        private readonly string _basePath;

        public GenomeManager()
        {
#if UNITY_EDITOR
            _basePath = Path.Combine(Application.dataPath, "..", "EditorLogs", GenomesFolder);
#else
            _basePath = Path.Combine(Application.persistentDataPath, GenomesFolder);
#endif
            Directory.CreateDirectory(_basePath);
        }

        /// <summary>Загружает лучший геном для уровня/стиля или null если не найден.</summary>
        public BotGenome Load(BotPlayStyle style, string levelName)
        {
            var path = GetPath(style, levelName);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<BotGenome>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GenomeManager] Failed to load genome: {e.Message}");
                return null;
            }
        }

        /// <summary>Сохраняет геном на диск.</summary>
        public void Save(BotGenome genome)
        {
            var path = GetPath(genome);
            try
            {
                var json = JsonUtility.ToJson(genome, true);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GenomeManager] Failed to save genome: {e.Message}");
            }
        }

        /// <summary>
        /// Сравнивает новый fitness с лучшим. Если лучше — сохраняет как best.
        /// Всегда обновляет LastFitness и историю.
        /// </summary>
        /// <returns>true если новый fitness стал лучшим.</returns>
        public bool SaveIfBetter(BotGenome genome, float newFitness)
        {
            genome.LastFitness = newFitness;
            genome.FitnessHistory.Add(newFitness);

            // Ограничиваем историю
            if (genome.FitnessHistory.Count > MaxHistorySize)
                genome.FitnessHistory.RemoveAt(0);

            bool improved = newFitness > genome.BestFitness;
            if (improved)
                genome.BestFitness = newFitness;

            Save(genome);
            return improved;
        }

        /// <summary>Удаляет геном (reset to preset).</summary>
        public void Delete(BotPlayStyle style, string levelName)
        {
            var path = GetPath(style, levelName);
            if (File.Exists(path))
                File.Delete(path);
        }

        // ──────────────── Path helpers ────────────────

        private string GetPath(BotGenome genome) =>
            GetPath(genome.PlayStyle, genome.LevelName);

        private string GetPath(BotPlayStyle style, string level) =>
            GetPath(style.ToString(), level);

        private string GetPath(string style, string level)
        {
            var safeName = level
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_")
                .Replace(" ", "_");
            return Path.Combine(_basePath, $"{style}_{safeName}.json");
        }
    }
}
