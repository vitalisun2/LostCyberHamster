using System.Collections.Generic;
using GameManagement;
using LostCyberHamster.UI;
using Vues.GameCore;

namespace Assets.Scripts.TutorialOld
{
    public static class TutorialSkinLessonSandboxActions
    {
        public static TutorialUiActionContext Execute(TutorialUiAction action)
        {
            switch (action)
            {
                case TutorialUiAction.BuySkin:
                    BuyElectricStrikeSkin();
                    return TutorialUiActionContext.ForSkin(TutorialMetaCoordinator.ElectricStrikeSkinId);
                case TutorialUiAction.EquipSkin:
                    EquipElectricStrikeSkin();
                    return TutorialUiActionContext.ForSkin(TutorialMetaCoordinator.ElectricStrikeSkinId);
                default:
                    return TutorialUiActionContext.Empty;
            }
        }

        public static bool Handles(TutorialUiAction action)
        {
            return action == TutorialUiAction.BuySkin || action == TutorialUiAction.EquipSkin;
        }

        private static void BuyElectricStrikeSkin()
        {
            int skinId = TutorialMetaCoordinator.ElectricStrikeSkinId;
            if (!GameDataManager.PlayerData.PurchasedSkinIds.Contains(skinId))
            {
                GameDataManager.PlayerData.PurchasedSkinIds = new List<int>(GameDataManager.PlayerData.PurchasedSkinIds)
                {
                    skinId
                };
            }

            ResourceManager.SpendResource(ResourceType.Crystals, TutorialMetaCoordinator.RewardCrystals);
            UIManager.OnRepaintScreen?.Invoke();
        }

        private static void EquipElectricStrikeSkin()
        {
            int skinId = TutorialMetaCoordinator.ElectricStrikeSkinId;
            if (!GameDataManager.PlayerData.PurchasedSkinIds.Contains(skinId))
            {
                GameDataManager.PlayerData.PurchasedSkinIds.Add(skinId);
            }

            GameDataManager.PlayerData.AppliedSkinId = skinId;
            UIManager.OnRepaintScreen?.Invoke();
        }
    }
}
