using System;

namespace LostCyberHamster.UI
{
    /// <summary>Данные двух вариантов обязательного выбора сохранения.</summary>
    public sealed class CloudSaveConflictModalDto
    {
        /// <summary>Создаёт модель облачной и локальной карточек.</summary>
        public CloudSaveConflictModalDto(
            CloudSaveConflictCardDto cloud,
            CloudSaveConflictCardDto thisDevice)
        {
            Cloud = cloud;
            ThisDevice = thisDevice ?? throw new ArgumentNullException(nameof(thisDevice));
        }

        public CloudSaveConflictCardDto Cloud { get; }
        public CloudSaveConflictCardDto ThisDevice { get; }
        public bool CanChooseCloud => Cloud != null;
    }
}
