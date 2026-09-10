using System;
using System.Linq;
using System.Threading.Tasks;
using GameManagement;
using Assets.Scripts.Tutorial;

namespace LostCyberHamster.UI
{
    /// <summary>Доставляет сохранённое повышение в безопасной точке; подтверждение не выдаёт награды.</summary>
    internal sealed class PlayerLevelPresentation : IDisposable
    {
        private readonly UIManager _ui;
        private bool _showing;
        private bool _disposed;
        private bool _loading;
        private int _version;

        public PlayerLevelPresentation(UIManager ui)
        {
            _ui = ui;
            GameDataManager.ProfileChanged += Cancel;
        }

        public void Dispose()
        {
            _disposed = true;
            GameDataManager.ProfileChanged -= Cancel;
            Cancel();
        }

        private void Cancel()
        {
            _version++;
            if (_showing) _ui.CloseModal(ScreenEnum.LevelUpModal);
            _showing = false;
            _loading = false;
            _ui.HasPriorityPresentation = false;
        }

        public static bool HasPendingLevel => GameDataManager.PlayerData != null &&
            GameDataManager.PlayerData.PlayerLevel > Math.Max(1,
                GameDataManager.PlayerData.LastAcknowledgedPlayerLevel);

        /// <summary>Показывает один накопленный диапазон, затем возвращает исходное продолжение.</summary>
        public async Task<bool> ShowAsync(Action continued, Action openShield = null,
            Func<bool, Action> prepareContinuation = null)
        {
            if (_showing && !_loading && _ui.CurrentModal != ScreenEnum.LevelUpModal) Cancel();
            if (_disposed || _showing || !HasPendingLevel || _ui.CurrentModal.HasValue)
                return false;

            // Подтверждение привязано к показанному профилю и верхней границе диапазона.
            var profile = GameDataManager.ProfileId;
            var generation = GameDataManager.Generation;
            var data = GameDataManager.PlayerData;
            int previous = Math.Max(1, data.LastAcknowledgedPlayerLevel);
            int current = data.PlayerLevel;
            var rewards = data.PendingLevelUpRewards?
                .Where(reward => reward.PlayerLevel > previous && reward.PlayerLevel <= current).ToArray();
            // У прежних повышений квитанций нет: исторически каждое уже выдало один DP.
            int points = current - previous - (rewards?.Length ?? 0) +
                (rewards?.Sum(reward => reward.DevelopmentPoints) ?? 0);
            int coins = rewards?.Sum(reward => reward.Coins) ?? 0;
            Action preparedContinuation = null;
            var modal = _ui.GetController<LevelUpModalController>();
            _showing = true;
            _loading = true;
            int version = ++_version;
            _ui.HasPriorityPresentation = true;
            try
            {
                modal.SetLevelUpData(previous, current, points, coins);
                modal.SetAcceptanceAction(CommitAcknowledgment);
                modal.SetOkAction(() => Finish(continued));
                modal.SetShieldAction(!data.HasUsedTutorialShield &&
                    (data.FirstSessionReturnToShield || previous < 2 && openShield != null) &&
                    (ShieldTutorialProgress.IsShieldUnlocked || data.DevelopmentPoints > 0)
                    ? () => Finish(openShield) : null);
                modal.SetDevelopmentAction(() => Finish(null));
                await _ui.ShowModalAsync(ScreenEnum.LevelUpModal);
                if (version != _version) return false;
                _loading = false;
                if (_disposed || profile != GameDataManager.ProfileId || generation != GameDataManager.Generation ||
                    _ui.CurrentModal != ScreenEnum.LevelUpModal)
                {
                    _showing = false;
                    _ui.HasPriorityPresentation = false;
                    return false;
                }
                FirstSessionTelemetry.Record("level_notification_shown", "level_up", current);
                return true;
            }
            catch
            {
                if (version == _version)
                {
                    _showing = false;
                    _loading = false;
                    _ui.HasPriorityPresentation = false;
                }
                throw;
            }

            bool CommitAcknowledgment(LevelUpAction choice)
            {
                if (!_showing || version != _version || profile != GameDataManager.ProfileId || generation != GameDataManager.Generation)
                    return false;
                bool acknowledged = false;
                GameDataManager.ExecuteTransaction(CheckpointReason.PlayerLevelPresentationAcknowledged,
                    () =>
                    {
                        preparedContinuation = choice == LevelUpAction.Development
                            ? FirstSessionNavigation.PrepareDevelopment(_ui)
                            : prepareContinuation?.Invoke(choice == LevelUpAction.Shield);
                        GameDataManager.PlayerData.LastAcknowledgedPlayerLevel =
                            Math.Max(GameDataManager.PlayerData.LastAcknowledgedPlayerLevel, current);
                        GameDataManager.PlayerData.PendingLevelUpRewards?.RemoveAll(reward => reward.PlayerLevel <= current);
                        acknowledged = true;
                    });
                if (!acknowledged) return false;
                FirstSessionTelemetry.Record("level_notification_ack", "level_up", current);
                return true;
            }

            void Finish(Action continuation)
            {
                _showing = false;
                _ui.HasPriorityPresentation = false;
                (preparedContinuation ?? continuation)?.Invoke();
            }
        }
    }
}
