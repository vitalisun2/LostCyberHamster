using System;
using System.Collections.Generic;
using GameManagement.Progress;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Представляет данные локации для экранов выбора и рейтинга.
    /// </summary>
    public sealed class LocationView
    {
        private readonly LocationProgress _progress;

        /// <summary>
        /// Создаёт UI-представление локации с готовым состоянием прогресса.
        /// </summary>
        public LocationView(
            string key,
            string displayName,
            string imageAddress,
            IReadOnlyList<PartView> parts,
            LocationProgress progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Key = key;
            DisplayName = displayName;
            ImageAddress = imageAddress;
            Parts = parts ?? Array.Empty<PartView>();
        }

        public int Index => _progress.LocationIndex;

        public string Id => _progress.LocationId;

        public string Key { get; }

        public string DisplayName { get; }

        public string ImageAddress { get; }

        public IReadOnlyList<PartView> Parts { get; }

        public bool IsUnlocked => _progress.IsUnlocked;

        public bool IsCompleted => _progress.IsCompleted;

        public bool IsMastered => _progress.IsMastered;

        public int TotalStars => _progress.TotalStars;

        public int TotalLevels => _progress.TotalLevels;
    }
}
