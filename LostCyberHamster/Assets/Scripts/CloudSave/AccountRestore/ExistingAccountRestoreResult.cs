namespace GameManagement.CloudSave
{
    /// <summary>Описывает итог входа в существующий аккаунт и восстановления его прогресса.</summary>
    public enum ExistingAccountRestoreResult
    {
        /// <summary>Account-сессия и cloud snapshot успешно приняты.</summary>
        Restored,

        /// <summary>Вход в существующую account-сессию не завершён.</summary>
        SignInFailed,

        /// <summary>Cloud snapshot выбранного аккаунта отсутствует.</summary>
        SnapshotMissing,

        /// <summary>Cloud snapshot принадлежит другому Player ID.</summary>
        OwnerMismatch,

        /// <summary>Данные cloud snapshot не прошли проверку.</summary>
        SnapshotRejected,

        /// <summary>Cloud snapshot не удалось загрузить.</summary>
        LoadFailed,

        /// <summary>Cloud snapshot не удалось применить локально.</summary>
        ApplyFailed
    }
}
