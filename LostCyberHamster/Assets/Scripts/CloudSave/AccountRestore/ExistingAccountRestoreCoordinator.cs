using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Завершает вход в существующий аккаунт только после успешного восстановления его данных.
    /// </summary>
    public sealed class ExistingAccountRestoreCoordinator
    {
        /// <summary>Управляет переходом между гостевой и связанной account-сессией.</summary>
        private readonly AccountService _accountService;

        /// <summary>Загружает и применяет cloud snapshot выбранного аккаунта.</summary>
        private readonly CloudSyncService _cloudSyncService;

        public ExistingAccountRestoreCoordinator(
            AccountService accountService,
            CloudSyncService cloudSyncService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _cloudSyncService = cloudSyncService ?? throw new ArgumentNullException(nameof(cloudSyncService));
        }

        /// <summary>Входит в существующий аккаунт только после успешного восстановления его прогресса.</summary>
        public async Task<ExistingAccountRestoreResult> RestoreAsync()
        {
            // Запускаем account flow и принимаем новую сессию только после cloud restore.
            var restoreResult = ExistingAccountRestoreResult.SignInFailed;
            var signedIn = await _accountService.SignInExistingAccountAsync(async playerId =>
            {
                // Загружаем прогресс новой сессии до её окончательного принятия.
                restoreResult = await _cloudSyncService.LoadExistingAccountAsync(playerId);
                return restoreResult == ExistingAccountRestoreResult.Restored;
            });

            // Возвращаем точный результат восстановления или сбой завершения входа.
            if (signedIn)
                return ExistingAccountRestoreResult.Restored;

            return restoreResult == ExistingAccountRestoreResult.Restored
                ? ExistingAccountRestoreResult.SignInFailed
                : restoreResult;
        }
    }
}
