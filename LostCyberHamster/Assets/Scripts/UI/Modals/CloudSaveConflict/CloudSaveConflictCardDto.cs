using System;

namespace LostCyberHamster.UI
{
    /// <summary>Данные одной карточки выбора сохранения.</summary>
    public sealed class CloudSaveConflictCardDto
    {
        /// <summary>Создаёт карточку с сохранёнными показателями прогресса.</summary>
        public CloudSaveConflictCardDto(
            int completedLevels,
            int money,
            int crystals,
            DateTime savedAt,
            int playerLevel = 1)
        {
            CompletedLevels = completedLevels;
            Money = money;
            Crystals = crystals;
            SavedAt = savedAt;
            PlayerLevel = playerLevel;
        }

        public int CompletedLevels { get; }
        public int Money { get; }
        public int Crystals { get; }
        public DateTime SavedAt { get; }
        public int PlayerLevel { get; }
    }
}
