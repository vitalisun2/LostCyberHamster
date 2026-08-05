using System;
using System.Collections.Generic;
using GameManagement.Progress;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Представляет данные части суток для экранов выбора и рейтинга.
    /// </summary>
    public sealed class PartView
    {
        private readonly PartProgress _progress;

        /// <summary>
        /// Создаёт UI-представление части суток с готовым состоянием прогресса.
        /// </summary>
        public PartView(
            string key,
            string displayName,
            PartProgress progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Key = key;
            DisplayName = displayName;
        }

        public int Index => _progress.PartIndex;

        public string Id => _progress.PartOfDayId;

        public string Key { get; }

        public string DisplayName { get; }

        public IReadOnlyList<LevelProgress> Levels => _progress.Levels;

        public bool IsUnlocked => _progress.IsUnlocked;

        public bool IsCompleted => _progress.IsCompleted;

        public bool IsMastered => _progress.IsMastered;

        public int TotalStars => _progress.TotalStars;

        public int TotalLevels => _progress.TotalLevels;
    }
}
