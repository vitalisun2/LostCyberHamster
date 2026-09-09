using GameManagement;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Описание и цена улучшения из того же каталога, что использует runtime.</summary>
    internal static class SuperAttackDescriptionFormatter
    {
        public static string Roman(int level) => level == 3 ? "III" : level == 2 ? "II" : "I";

        public static string Describe(SuperAttackData ability, int level)
        {
            var data = SuperAttackLevelResolver.Get(ability, level);
            switch (ability.Id)
            {
                case 1:
                    return data.DestroysOnCollision
                        ? Format("progression_shield_destroy", data.Duration, data.DropChance * 100, data.MaximumDrops)
                        : Format("progression_shield_protect", data.Duration);
                case 2:
                    return Format("progression_lightning", data.RangeMultiplier * 100, data.DropChance * 100, data.MaximumDrops);
                case 3:
                    return Format("progression_skate", data.Duration, data.JumpCombinations);
                default:
                    return LocalizationManager.GetLocalizedString(ability.DescriptionLocalizationKey);
            }
        }

        public static string Upgrade(SuperAttackData ability, int level)
        {
            if (level >= SuperAttackLevelResolver.MaximumLevel) return Format("progression_maximum");
            string next = Roman(level + 1) + ": " + Describe(ability, level + 1);
            if (GameDataManager.PlayerData.PlayerLevel < SuperAttackLevelResolver.UpgradePlayerLevel)
                return next + "\n" + Format("progression_upgrade_level", SuperAttackLevelResolver.UpgradePlayerLevel);
            return next + "\n" + Format(GameDataManager.PlayerData.DevelopmentPoints > 0
                ? "progression_upgrade_action" : "progression_upgrade_points");
        }

        public static string Compact(SuperAttackData ability, int level)
        {
            var data = SuperAttackLevelResolver.Get(ability, level);
            if (ability.Id == 3) return Format("progression_skate_compact", data.Duration, data.JumpCombinations);
            return Format(ability.Id == 1 ? "progression_shield_compact" : "progression_lightning_compact",
                ability.Id == 1 ? data.Duration : data.RangeMultiplier * 100, data.DropChance * 100, data.MaximumDrops);
        }

        public static string UpgradeAction(int level)
        {
            if (level >= SuperAttackLevelResolver.MaximumLevel) return Format("progression_maximum");
            if (GameDataManager.PlayerData.PlayerLevel < SuperAttackLevelResolver.UpgradePlayerLevel)
                return Format("progression_upgrade_level", SuperAttackLevelResolver.UpgradePlayerLevel);
            return Format(GameDataManager.PlayerData.DevelopmentPoints > 0
                ? "progression_upgrade_action" : "progression_upgrade_points");
        }

        private static string Format(string key, params object[] values) =>
            string.Format(LocalizationManager.GetLocalizedString(key), values);
    }
}
