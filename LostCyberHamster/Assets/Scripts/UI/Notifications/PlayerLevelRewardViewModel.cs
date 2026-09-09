using System.Collections.Generic;
using System.Linq;
using GameManagement;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Предлагает реальные открытия и улучшения, не изменяя каталог или баланс.</summary>
    internal static class PlayerLevelRewardViewModel
    {
        public static IReadOnlyList<string> GetOptions()
        {
            var result = new List<string>();
            var skins = SkinManager.AvailableSkins.Where(skin => skin.Id != CharacterDevelopmentService.DefaultSkinId).ToArray();
            var abilities = SuperAttackService.Items;
            for (int index = 0; index < abilities.Count || index < skins.Length; index++)
            {
                if (index < abilities.Count)
                {
                    var ability = abilities[index];
                    string name = LocalizationManager.GetLocalizedString(ability.NameLocalizationKey);
                    int level = SuperAttackLevelResolver.GetLevel(GameDataManager.PlayerData, ability.Id);
                    if (CharacterDevelopmentService.CanUnlockSuperAttack(ability.Id))
                        result.Add(Format("progression_unlock_option", name));
                    else if (CharacterDevelopmentService.CanUpgradeSuperAttack(ability.Id, level))
                        result.Add(Format("progression_upgrade_option", name, level + 1));
                }
                if (index < skins.Length && CharacterDevelopmentService.CanUnlockSkin(skins[index].Id))
                    result.Add(Format("progression_skin_option", skins[index].Name, skins[index].Price));
            }
            return result.Take(3).ToArray();
        }

        private static string Format(string key, params object[] values) =>
            string.Format(LocalizationManager.GetLocalizedString(key), values);
    }
}
