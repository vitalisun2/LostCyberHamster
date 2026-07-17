using System.Collections.Generic;
using UnityEngine;

namespace GameManagement.Progress
{
    /// <summary>
    /// Хранит в памяти лучшие результаты уровней и считает результат части дня.
    /// </summary>
    public static class PartOfDayScoreService
    {
        private static readonly Dictionary<LevelProgressKey, int> _levelBestScores = new();

        /// <summary>
        /// Учитывает результат успешного забега и возвращает текущую сумму части дня.
        /// </summary>
        public static int RecordSuccessfulRun(LevelProgressKey levelKey, int runScore)
        {
            _levelBestScores.TryGetValue(levelKey, out var oldLevelBestScore);
            var oldPartOfDayTotalScore = CalculatePartOfDayTotalScore(levelKey);
            var isUpdated = runScore > oldLevelBestScore;

            if (isUpdated)
            {
                _levelBestScores[levelKey] = runScore;
            }

            var newLevelBestScore = isUpdated ? runScore : oldLevelBestScore;
            var newPartOfDayTotalScore = oldPartOfDayTotalScore + newLevelBestScore - oldLevelBestScore;
            var status = isUpdated ? "updated" : "ignored";

            Debug.Log(
                $"[PartOfDayScore] level={levelKey} runScore={runScore} " +
                $"levelBestScore={oldLevelBestScore}->{newLevelBestScore} " +
                $"partOfDayTotalScore={oldPartOfDayTotalScore}->{newPartOfDayTotalScore} status={status}");

            return newPartOfDayTotalScore;
        }

        private static int CalculatePartOfDayTotalScore(LevelProgressKey levelKey)
        {
            var totalScore = 0;

            foreach (var entry in _levelBestScores)
            {
                if (entry.Key.BelongsToPart(levelKey.LocationId, levelKey.PartOfDayId))
                {
                    totalScore += entry.Value;
                }
            }

            return totalScore;
        }
    }
}
