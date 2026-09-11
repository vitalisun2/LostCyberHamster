using System;
using System.Collections.Generic;
using System.Linq;
using GameManagement;
using GameManagement.Progress;

namespace Vues.GameCore
{
    /// <summary>
    /// Управляет Development Points и открытиями элементов развития персонажа.
    /// </summary>
    public static class CharacterDevelopmentService
    {
        public const int CurrentProgressVersion = 3;
        public const int DefaultSkinId = SkinIdentity.DefaultId;
        public const int CompletedDevelopmentLevelUpCoins = 50;

        /// <summary>Проверяет все открытия и tiers загруженных каталогов по переданному профилю.</summary>
        public static bool IsDevelopmentComplete(PlayerData player)
        {
            if (player == null || !SkinManager.IsCatalogLoaded || !SuperAttackService.IsCatalogLoaded)
                return false;

            // Покупка уже открытого скина за кристаллы не расходует DP.
            return SkinManager.AvailableSkins.All(skin => skin.Id == DefaultSkinId ||
                    Contains(player.UnlockedSkinIds, skin.Id)) &&
                SuperAttackService.Items.All(ability => Contains(player.UnlockedSuperAttackIds, ability.Id) &&
                    ability.Levels?.Length > 0 && SuperAttackLevelResolver.GetLevel(player, ability.Id) >=
                    ability.Levels.Max(tier => tier.Level));
        }

        public static int DevelopmentPoints =>
            GameDataManager.PlayerData?.DevelopmentPoints ?? 0;

        /// <summary>
        /// Проверяет persisted-открытие скина.
        /// </summary>
        public static bool IsSkinUnlocked(int skinId)
        {
            return skinId == DefaultSkinId ||
                   Contains(
                       GameDataManager.PlayerData?.UnlockedSkinIds,
                       skinId);
        }

        /// <summary>
        /// Проверяет persisted-открытие суперспособности.
        /// </summary>
        public static bool IsSuperAttackUnlocked(int superAttackId)
        {
            return Contains(
                GameDataManager.PlayerData?.UnlockedSuperAttackIds,
                superAttackId);
        }

        /// <summary>
        /// Проверяет, является ли скин следующим закрытым элементом каталога.
        /// </summary>
        public static bool CanUnlockSkin(int skinId)
        {
            var playerData = GameDataManager.PlayerData;
            return playerData?.UnlockedSkinIds != null &&
                   playerData.DevelopmentPoints > 0 &&
                   !IsSkinUnlocked(skinId) &&
                   SkinManager.AvailableSkins
                       .FirstOrDefault(skin => !IsSkinUnlocked(skin.Id))
                       ?.Id == skinId;
        }

        /// <summary>
        /// Проверяет, является ли способность следующим закрытым элементом каталога.
        /// </summary>
        public static bool CanUnlockSuperAttack(int superAttackId)
        {
            var playerData = GameDataManager.PlayerData;
            return playerData?.UnlockedSuperAttackIds != null &&
                   playerData.DevelopmentPoints > 0 &&
                   !IsSuperAttackUnlocked(superAttackId) &&
                   SuperAttackService.Items
                       .FirstOrDefault(
                           ability => !IsSuperAttackUnlocked(ability.Id))
                       ?.Id == superAttackId;
        }

        /// <summary>
        /// Тратит один Development Point и открывает скин из production catalog.
        /// </summary>
        public static bool TryUnlockSkin(int skinId)
        {
            var playerData = GameDataManager.PlayerData;
            if (!CanUnlockSkin(skinId))
            {
                return false;
            }

            return TryUnlock(
                skinId,
                playerData.UnlockedSkinIds);
        }

        /// <summary>
        /// Тратит один Development Point и открывает суперспособность из production catalog.
        /// </summary>
        public static bool TryUnlockSuperAttack(int superAttackId)
        {
            var playerData = GameDataManager.PlayerData;
            if (!CanUnlockSuperAttack(superAttackId))
            {
                return false;
            }

            return TryUnlock(superAttackId, playerData.UnlockedSuperAttackIds, () =>
                playerData.SuperAttackLevels.Add(new SuperAttackLevelProgress { SuperAttackId = superAttackId }));
        }

        /// <summary>Проверяет последовательное улучшение уже открытой способности.</summary>
        public static bool CanUpgradeSuperAttack(int abilityId, int expectedLevel)
        {
            var player = GameDataManager.PlayerData;
            return player != null && player.DevelopmentPoints > 0 && IsSuperAttackUnlocked(abilityId) &&
                SuperAttackService.TryGet(abilityId, out _) && expectedLevel >= 1 &&
                expectedLevel < SuperAttackLevelResolver.MaximumLevel &&
                SuperAttackLevelResolver.GetLevel(player, abilityId) == expectedLevel;
        }

        /// <summary>Списывает одно очко и сохраняет следующий уровень; повтор старой кнопки отклоняется.</summary>
        public static bool TryUpgradeSuperAttack(int abilityId, int expectedLevel)
        {
            if (!CanUpgradeSuperAttack(abilityId, expectedLevel)) return false;
            GameDataManager.ExecuteTransaction(CheckpointReason.CharacterDevelopmentUpgraded, () =>
            {
                if (!CanUpgradeSuperAttack(abilityId, expectedLevel))
                    throw new InvalidOperationException("Ability upgrade context changed.");
                var player = GameDataManager.PlayerData;
                var progress = player.SuperAttackLevels.FirstOrDefault(item => item.SuperAttackId == abilityId);
                if (progress == null)
                {
                    progress = new SuperAttackLevelProgress { SuperAttackId = abilityId };
                    player.SuperAttackLevels.Add(progress);
                }
                player.DevelopmentPoints--;
                progress.Level = expectedLevel + 1;
            });
            return true;
        }

        /// <summary>
        /// Начисляет DP либо монеты за повышения и сохраняет факт внутри транзакции владельца XP.
        /// </summary>
        internal static void GrantForLevelUps(
            PlayerData playerData,
            int levelsGained)
        {
            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            if (levelsGained < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelsGained));
            }

            if (playerData.DevelopmentPoints < 0)
            {
                throw new InvalidOperationException(
                    "Development Points must be normalized before level-up.");
            }

            if (levelsGained == 0) return;

            // Рассчитываем весь диапазон до изменения балансов.
            bool complete = IsDevelopmentComplete(playerData);
            int points = complete ? 0 : levelsGained;
            int coins = complete ? checked(CompletedDevelopmentLevelUpCoins * levelsGained) : 0;
            int updatedPoints = checked(playerData.DevelopmentPoints + points);
            int updatedMoney = checked(playerData.Money + coins);
            playerData.PendingLevelUpRewards ??= new List<LevelUpReward>();
            for (int offset = 0; offset < levelsGained; offset++)
                playerData.PendingLevelUpRewards.Add(new LevelUpReward
                {
                    PlayerLevel = playerData.PlayerLevel - levelsGained + offset + 1,
                    DevelopmentPoints = complete ? 0 : 1,
                    Coins = complete ? CompletedDevelopmentLevelUpCoins : 0
                });
            playerData.DevelopmentPoints = updatedPoints;
            playerData.Money = updatedMoney;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Открывает скин без расхода points для production-backed DEV tools.
        /// </summary>
        public static bool UnlockSkinForTesting(int skinId)
        {
            if (SkinManager.AvailableSkins.All(skin => skin.Id != skinId) ||
                IsSkinUnlocked(skinId))
            {
                return false;
            }

            GameDataManager.PlayerData.UnlockedSkinIds.Add(skinId);
            return true;
        }

        /// <summary>
        /// Открывает суперспособность без расхода points для production-backed DEV tools.
        /// </summary>
        public static bool UnlockSuperAttackForTesting(int superAttackId)
        {
            if (!SuperAttackService.TryGet(superAttackId, out _) ||
                IsSuperAttackUnlocked(superAttackId))
            {
                return false;
            }

            GameDataManager.PlayerData.UnlockedSuperAttackIds.Add(
                superAttackId);
            GameDataManager.PlayerData.SuperAttackLevels.Add(new SuperAttackLevelProgress { SuperAttackId = superAttackId });
            return true;
        }
#endif

        private static bool TryUnlock(
            int id,
            ICollection<int> unlockedIds, Action onUnlocked = null)
        {
            var playerData = GameDataManager.PlayerData;
            if (playerData == null ||
                unlockedIds == null ||
                unlockedIds.Contains(id) ||
                playerData.DevelopmentPoints <= 0)
            {
                return false;
            }

            // Списываем point и открываем элемент одним persisted checkpoint.
            GameDataManager.ExecuteTransaction(CheckpointReason.CharacterDevelopmentUnlocked, () =>
            {
                playerData.DevelopmentPoints--;
                unlockedIds.Add(id);
                onUnlocked?.Invoke();
            });
            return true;
        }

        private static bool Contains(
            IReadOnlyCollection<int> values,
            int value)
        {
            return values?.Contains(value) == true;
        }
    }
}
