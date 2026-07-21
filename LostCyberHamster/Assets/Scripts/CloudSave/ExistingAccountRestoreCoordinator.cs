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
        private readonly AccountService _accountService;
        private readonly CloudSyncService _cloudSyncService;

        public ExistingAccountRestoreCoordinator(
            AccountService accountService,
            CloudSyncService cloudSyncService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _cloudSyncService = cloudSyncService ?? throw new ArgumentNullException(nameof(cloudSyncService));
        }

        public async Task<ExistingAccountRestoreResult> RestoreAsync()
        {
            var restoreResult = ExistingAccountRestoreResult.SignInFailed;
            var signedIn = await _accountService.SignInExistingAccountAsync(async playerId =>
            {
                restoreResult = await _cloudSyncService.LoadExistingAccountAsync(playerId);
                return restoreResult == ExistingAccountRestoreResult.Restored;
            });

            return signedIn
                ? ExistingAccountRestoreResult.Restored
                : restoreResult;
        }
    }
}
