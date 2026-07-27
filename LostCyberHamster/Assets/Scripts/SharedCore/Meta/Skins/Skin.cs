using GameManagement;
using UnityEngine;
using Vues.GameCore;

public class Skin
{
    public int Id;
    public string Name => LocalizationManager.GetLocalizedString(NameLocalizationKey) ?? NameLocalizationKey;
    public string NameLocalizationKey;
    public int Price;
    public ResourceType PriceType;
    public Sprite HamsterSprite;
    public RuntimeAnimatorController HamsterOverrideController;

    public bool IsPurchased => GameDataManager.PlayerData.PurchasedSkinIds.Contains(Id);
}

