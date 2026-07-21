using System;

namespace LostCyberHamster.UI
{
    /// <summary>Данные одной карточки выбора сохранения.</summary>
    public sealed class CloudSaveConflictCardData
    {
        /// <summary>Создаёт карточку с сохранёнными показателями прогресса.</summary>
        public CloudSaveConflictCardData(
            int completedLevels,
            int money,
            int crystals,
            DateTime savedAt)
        {
            CompletedLevels = completedLevels;
            Money = money;
            Crystals = crystals;
            SavedAt = savedAt;
        }

        public int CompletedLevels { get; }
        public int Money { get; }
        public int Crystals { get; }
        public DateTime SavedAt { get; }
    }
}
