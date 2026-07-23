using System;
using System.Threading.Tasks;
using GameManagement.CloudSave;

namespace Assets.Scripts.Account
{
    /// <summary>
    /// Завершает вход в существующий аккаунт только после успешного восстановления его данных.
    /// </summary>
    public sealed class ExistingAccountRestoreCoordinator
    {
        /// <summary>Управляет переходом между гостевой и связанной с аккаунтом сессией.</summary>
        private readonly AccountService _accountService;

        /// <summary>Загружает и применяет облачный снимок выбранного аккаунта.</summary>
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
            // Запускаем вход и принимаем новую сессию только после облачного восстановления.
            var signedIn = await _accountService.SignInExistingAccountAsync(async playerId =>
            {
                // Загружаем прогресс новой сессии до её окончательного принятия.
                await _cloudSyncService.RestoreProgressAsync(playerId);
                return true;
            });

            // Неудачный вход означает отказ восстановления.
            return signedIn
                ? ExistingAccountRestoreResult.Restored
                : ExistingAccountRestoreResult.SignInFailed;
        }
    }
}
