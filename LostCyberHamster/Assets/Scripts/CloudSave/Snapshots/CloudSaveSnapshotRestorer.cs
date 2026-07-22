using System;

namespace GameManagement.CloudSave
{
    /// <summary>Восстанавливает и проверяет PlayerData из облачного снимка.</summary>
    internal static class CloudSaveSnapshotRestorer
    {
        /// <summary>Безопасно восстанавливает валидные данные и исправляет допустимые повреждения.</summary>
        internal static bool TryRestore(
            CloudSaveSnapshotDto snapshot,
            out PlayerData restoredData,
            out string rejectionReason)
        {
            // Готовим безопасный результат отказа.
            restoredData = null;
            rejectionReason = string.Empty;

            try
            {
                // Восстанавливаем и проверяем данные снимка.
                var candidate = CloudSaveSnapshotCodec.RestorePlayerData(snapshot);
                var validation = PlayerDataValidator.Validate(candidate);

                // Исправляем только безопасно восстанавливаемые данные.
                if (validation.Status == PlayerDataValidationStatus.Repairable)
                {
                    PlayerDataValidator.RepairSafe(candidate, validation);
                    validation = PlayerDataValidator.Validate(candidate);
                }

                // Отклоняем данные, которые не удалось привести к валидному состоянию.
                if (validation.Status != PlayerDataValidationStatus.Valid)
                {
                    rejectionReason = validation.Reason;
                    return false;
                }

                // Возвращаем проверенные данные.
                restoredData = candidate;
                return true;
            }
            catch (Exception exception)
            {
                // Преобразуем ошибку чтения в контролируемый отказ.
                rejectionReason = exception.GetType().Name;
                return false;
            }
        }
    }
}
