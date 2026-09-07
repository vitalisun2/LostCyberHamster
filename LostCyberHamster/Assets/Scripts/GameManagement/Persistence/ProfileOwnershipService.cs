using System;
using Assets.Scripts.Account;
using GameManagement.CloudSave;

namespace GameManagement
{
    /// <summary>Принимает старый локальный прогресс явным выбором текущего гостя.</summary>
    public sealed class ProfileOwnershipService : IDisposable
    {
        private readonly AccountService _account;
        private readonly CloudSyncService _cloud;
        private bool _disposed;

        public static ProfileOwnershipService Instance { get; private set; }
        public string GuestName => _account.PlayerName;
        public string GuestPlayerId => _account.TryGetAuthenticatedPlayerId(out var playerId) ? playerId : null;
        public bool CanAdoptGuestProgress => !_disposed && _account.State == AccountState.Guest &&
            _account.TryGetAuthenticatedPlayerId(out _) && GameDataManager.OwnerPlayerId == null &&
            GameDataManager.IsLegacyOwnerUnassigned && GameDataManager.CanApplyCloudProgress &&
            !_cloud.HasUnresolvedConflict && !AccountTransitionScope.IsActive;

        public ProfileOwnershipService(AccountService account, CloudSyncService cloud)
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            Instance = this;
        }

        /// <summary>Принимает текущий снимок; UI передаёт зафиксированные при показе значения в перегрузку.</summary>
        public bool TryAdoptGuestProgress() => TryAdoptGuestProgress(GameDataManager.ProfileId,
            GameDataManager.Generation, GuestPlayerId, GameDataManager.LocalRevision);

        /// <summary>Атомарно сохраняет выбранного владельца, если показанные профиль и сессия ещё актуальны.</summary>
        public bool TryAdoptGuestProgress(string expectedProfileId, long expectedGeneration,
            string expectedPlayerId, long expectedRevision)
        {
            if (!CanAdoptGuestProgress || GameDataManager.ProfileId != expectedProfileId ||
                GameDataManager.Generation != expectedGeneration || GameDataManager.LocalRevision != expectedRevision ||
                !_account.TryGetAuthenticatedPlayerId(out var playerId) || playerId != expectedPlayerId)
                return false;
            GameDataManager.BindOwner(playerId, allowLegacyAdoption: true);
            return GameDataManager.OwnerPlayerId == playerId && !GameDataManager.IsLegacyOwnerUnassigned;
        }

        public void Dispose()
        {
            _disposed = true;
            if (Instance == this) Instance = null;
        }
    }
}
