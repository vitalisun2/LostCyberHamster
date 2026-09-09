using System.Collections.Generic;
using System.Globalization;
using GameManagement;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Использует реальные условия открытия, улучшения и покупки без второй экономики.</summary>
    internal sealed class DevelopmentNextGoalRule : INextGoalRule
    {
        public NextGoalKind Kind => NextGoalKind.Development;

        public void Collect(List<NextGoalCandidate> candidates, NextGoalConfiguration configuration)
        {
            // Способности остаются одной категорией независимо от конкретного суперудара.
            foreach (var ability in SuperAttackService.Items)
            {
                NextGoalAction action;
                string title;
                var screen = ScreenEnum.CharacterDevelopmentScreen;
                if (CharacterDevelopmentService.CanUnlockSuperAttack(ability.Id))
                { action = NextGoalAction.UnlockAbility; title = "next_goal_unlock_ability"; }
                else if (CharacterDevelopmentService.CanUpgradeSuperAttack(ability.Id,
                    SuperAttackLevelResolver.GetLevel(GameDataManager.PlayerData, ability.Id)))
                { action = NextGoalAction.UpgradeAbility; title = "next_goal_upgrade_ability"; screen = ScreenEnum.CharacterScreen; }
                else if (SuperAttackService.ActiveSuperAttackId == null && SuperAttackService.IsUnlocked(ability.Id))
                { action = NextGoalAction.EquipAbility; title = "next_goal_equip_ability"; screen = ScreenEnum.CharacterScreen; }
                else continue;
                candidates.Add(new NextGoalCandidate(Kind, action, ability.Id.ToString(CultureInfo.InvariantCulture),
                    NextGoalText.Get(title, NextGoalText.Get(ability.NameLocalizationKey)), null,
                    NextGoalText.Get(screen == ScreenEnum.CharacterScreen ? "first_session_hero" : "first_session_skills"), screen));
            }

            // Открытие скина за point и последующая покупка за валюту имеют разные подписи и переходы.
            foreach (var skin in SkinManager.AvailableSkins)
            {
                bool unlock = CharacterDevelopmentService.CanUnlockSkin(skin.Id);
                if (!unlock && !SkinManager.CanPurchaseSkin(skin.Id)) continue;
                candidates.Add(new NextGoalCandidate(Kind, unlock ? NextGoalAction.UnlockSkin : NextGoalAction.BuySkin,
                    skin.Id.ToString(CultureInfo.InvariantCulture), NextGoalText.Get(unlock ? "next_goal_unlock_skin" : "next_goal_buy_skin", skin.Name),
                    null, NextGoalText.Get(unlock ? "first_session_skills" : "first_session_hero"),
                    unlock ? ScreenEnum.CharacterDevelopmentScreen : ScreenEnum.CharacterScreen, icon: skin.HamsterSprite));
            }
        }
    }
}
