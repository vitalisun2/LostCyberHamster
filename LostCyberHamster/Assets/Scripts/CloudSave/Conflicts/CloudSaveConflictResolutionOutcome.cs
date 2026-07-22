using System;

namespace GameManagement.CloudSave
{
    /// <summary>Описывает итог попытки разрешить конкретный конфликт облачного сохранения.</summary>
    internal sealed class CloudSaveConflictResolutionOutcome<TVersion>
        where TVersion : class
    {
        private CloudSaveConflictResolutionOutcome(
            CloudSaveConflictModel conflict,
            TVersion version,
            bool isSuccessful)
        {
            // Сохраняем согласованные данные результата.
            Conflict = conflict;
            Version = version;
            IsSuccessful = isSuccessful;
        }

        /// <summary>Конфликт, для которого выполнялась попытка разрешения.</summary>
        public CloudSaveConflictModel Conflict { get; }

        /// <summary>Подтверждённая облачная версия при успешном разрешении.</summary>
        public TVersion Version { get; }

        /// <summary>Показывает, что выбранная ветка успешно применена или записана.</summary>
        public bool IsSuccessful { get; }

        /// <summary>Создаёт успешный результат с обязательными конфликтом и версией.</summary>
        public static CloudSaveConflictResolutionOutcome<TVersion> Success(
            CloudSaveConflictModel conflict,
            TVersion version)
        {
            // Проверяем обязательные данные успешного результата.
            if (conflict == null)
                throw new ArgumentNullException(nameof(conflict));
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            // Возвращаем завершённый успешный результат.
            return new CloudSaveConflictResolutionOutcome<TVersion>(
                conflict,
                version,
                isSuccessful: true);
        }

        /// <summary>Создаёт неуспешный результат для известного или отсутствующего конфликта.</summary>
        public static CloudSaveConflictResolutionOutcome<TVersion> Failure(
            CloudSaveConflictModel conflict)
        {
            return new CloudSaveConflictResolutionOutcome<TVersion>(
                conflict,
                version: null,
                isSuccessful: false);
        }
    }
}
