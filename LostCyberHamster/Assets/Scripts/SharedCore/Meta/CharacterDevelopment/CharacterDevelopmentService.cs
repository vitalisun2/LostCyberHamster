using System;
using System.Collections.Generic;
using System.Linq;
using GameManagement;

namespace Vues.GameCore
{
    /// <summary>
    /// Управляет Development Points и открытиями элементов развития персонажа.
    /// </summary>
    public static class CharacterDevelopmentService
    {
        public const int CurrentProgressVersion = 1;
        public const int DefaultSkinId = 0;

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
        /// Тратит один Development Point и открывает скин из production catalog.
        /// </summary>
        public static bool TryUnlockSkin(int skinId)
        {
            var playerData = GameDataManager.PlayerData;
            if (playerData?.UnlockedSkinIds == null ||
                SkinManager.AvailableSkins.All(skin => skin.Id != skinId))
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
            if (playerData?.UnlockedSuperAttackIds == null ||
                !SuperAttackService.TryGet(superAttackId, out _))
            {
                return false;
            }

            return TryUnlock(
                superAttackId,
                playerData.UnlockedSuperAttackIds);
        }

        /// <summary>
        /// Начисляет по одному Development Point за каждый фактический level-up.
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

            playerData.DevelopmentPoints = checked(
                playerData.DevelopmentPoints + levelsGained);
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
            return true;
        }
#endif

        private static bool TryUnlock(
            int id,
            ICollection<int> unlockedIds)
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
            playerData.DevelopmentPoints--;
            unlockedIds.Add(id);
            PlayerProgressCommitter.Commit(
                CheckpointReason.CharacterDevelopmentUnlocked);
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
