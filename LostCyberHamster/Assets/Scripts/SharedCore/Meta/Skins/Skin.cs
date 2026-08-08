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
    public string SkinVisualAddress;

    public bool IsPurchased => GameDataManager.PlayerData.PurchasedSkinIds.Contains(Id);
}
