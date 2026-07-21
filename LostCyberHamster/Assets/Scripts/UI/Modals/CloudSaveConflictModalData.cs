using System;

namespace LostCyberHamster.UI
{
    /// <summary>Данные двух вариантов обязательного выбора сохранения.</summary>
    public sealed class CloudSaveConflictModalData
    {
        /// <summary>Создаёт модель облачной и локальной карточек.</summary>
        public CloudSaveConflictModalData(
            CloudSaveConflictCardData cloud,
            CloudSaveConflictCardData thisDevice)
        {
            Cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            ThisDevice = thisDevice ?? throw new ArgumentNullException(nameof(thisDevice));
        }

        public CloudSaveConflictCardData Cloud { get; }
        public CloudSaveConflictCardData ThisDevice { get; }
    }
}
