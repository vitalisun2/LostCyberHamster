using GameManagement;
using Vues.GameCore;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Сохраняет только постоянные этапы урока; открытие и экипировка принадлежат игровым сервисам.</summary>
    public static class ShieldTutorialProgress
    {
        public const int ShieldId = 1;

        public static bool IsPending => GameDataManager.PlayerData != null &&
            GameDataManager.PlayerData.IsShieldTutorialStarted &&
            !GameDataManager.PlayerData.HasUsedTutorialShield;

        public static bool IsShieldUnlocked => SuperAttackService.IsUnlocked(ShieldId);

        public static bool IsShieldEquipped => GameDataManager.PlayerData != null &&
            SuperAttackService.ActiveSuperAttackId == ShieldId && IsShieldUnlocked;

        /// <summary>Читает эффективную длительность того же уровня, который активирует EnergyShieldAttack.</summary>
        public static bool TryGetShieldDuration(out float duration)
        {
            duration = 0;
            if (!SuperAttackService.TryGet(ShieldId, out var data)) return false;
            duration = SuperAttackLevelResolver.GetEffective(data).Duration;
            return true;
        }

        /// <summary>Запоминает начало реального открытия/экипировки без изменения валют или предметов.</summary>
        public static void MarkStarted()
        {
            var data = GameDataManager.PlayerData;
            if (data == null || data.IsShieldTutorialStarted || data.HasUsedTutorialShield ||
                TutorialStorage.IsPlayerDataBackupActive ||
                TutorialConstants.IsTutorialLevel(data.CurrentLevel)) return;

            GameDataManager.ExecuteTransaction(CheckpointReason.FirstSessionTutorialProgressed,
                () => GameDataManager.PlayerData.IsShieldTutorialStarted = true);
        }

        /// <summary>Фиксирует успешное применение щита; вызывается после подтверждённого UltaUsed.</summary>
        public static void MarkUsed()
        {
            if (GameDataManager.PlayerData == null || GameDataManager.PlayerData.HasUsedTutorialShield || !IsShieldEquipped ||
                TutorialStorage.IsPlayerDataBackupActive ||
                TutorialConstants.IsTutorialLevel(GameDataManager.PlayerData.CurrentLevel)) return;

            GameDataManager.ExecuteTransaction(CheckpointReason.FirstSessionTutorialProgressed,
                () =>
                {
                    GameDataManager.PlayerData.IsShieldTutorialStarted = true;
                    GameDataManager.PlayerData.HasUsedTutorialShield = true;
                },
                () => FirstSessionTelemetry.Record("shield_used"));
        }
    }
}
