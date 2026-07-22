namespace Assets.Scripts.Account
{
    /// <summary>Описывает итог входа в существующий аккаунт и восстановления его прогресса.</summary>
    public enum ExistingAccountRestoreResult
    {
        /// <summary>Сессия аккаунта и облачный снимок успешно приняты.</summary>
        Restored,

        /// <summary>Вход в существующий аккаунт не завершён.</summary>
        SignInFailed,

        /// <summary>Облачный снимок выбранного аккаунта отсутствует.</summary>
        SnapshotMissing,

        /// <summary>Облачный снимок принадлежит другому игроку.</summary>
        OwnerMismatch,

        /// <summary>Данные облачного снимка не прошли проверку.</summary>
        SnapshotRejected,

        /// <summary>Облачный снимок не удалось загрузить.</summary>
        LoadFailed,

        /// <summary>Облачный снимок не удалось применить локально.</summary>
        ApplyFailed
    }
}
