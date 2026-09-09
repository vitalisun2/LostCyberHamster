using System;
using System.Linq;
using GameManagement;

namespace Vues.GameCore
{
    /// <summary>Разрешает эффективные параметры из единого каталога и сохранённого уровня.</summary>
    public static class SuperAttackLevelResolver
    {
        public const int MaximumLevel = 3;
        public const int UpgradePlayerLevel = 7;

        public static int GetLevel(PlayerData player, int abilityId) =>
            player?.SuperAttackLevels?.FirstOrDefault(item => item.SuperAttackId == abilityId)?.Level ?? 1;

        public static SuperAttackLevelData GetEffective(SuperAttackData ability) =>
            Get(ability, GetLevel(GameDataManager.PlayerData, ability.Id));

        public static SuperAttackLevelData Get(SuperAttackData ability, int level)
        {
            return ability?.Levels?.FirstOrDefault(item => item.Level == level) ??
                throw new InvalidOperationException($"Способность {ability?.Id}: отсутствует уровень {level}.");
        }
    }
}
