using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.System;
using Atomic.Elements;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vues.GameCore;

public static class SkinManager
{
    public static List<string> AvailableSkinsNames => _availableSkins.Select(x => x.Name).ToList();
    public static string CurrentSkinName => CurrentSkin?.Name;
    public static bool IsUltaActive => CurrentSkin?.IsUltaActive.Value ?? false;
    public static int UltaChargeAmount => LevelController.Instance.LevelData.Hamster?.UltaChargeAmount.Value ?? 0;


    public static List<Skin> AvailableSkins => _availableSkins;

    public static Skin CurrentSkin => _availableSkins.FirstOrDefault(x => x.Id == GameDataManager.PlayerData.AppliedSkinId);

    private static List<Skin> _availableSkins = new();

    public static async Task Init()
    {
        string json = await LoadJsonFromAddressables();

        var skinDatas = JsonUtility.FromJson<SkinDataList>(json).skins;

        if (skinDatas == null)
        {
            Debug.LogError("Failed to deserialize skins from JSON.");
            return;
        }

        foreach (var skinData in skinDatas)
        {
            var skin = await SkinFactory.CreateSkinAsync(skinData);
            _availableSkins.Add(skin);
        }
    }

    public static bool CanPurchaseSkin(int skinId)
    {
        var skin = _availableSkins.FirstOrDefault(x => x.Id == skinId);
        return ResourceManager.CanSpendResource(skin.PriceType, skin.Price);
    }

    public static void PurchaseSkin(int skinId)
    {
        var skin = _availableSkins.FirstOrDefault(x => x.Id == skinId);
        
        if(ResourceManager.CanSpendResource(skin.PriceType, skin.Price) == false)
        {
            Debug.LogWarning("Not enough resources to purchase skin.");
            return;
        }
        ResourceManager.SpendResource(skin.PriceType, skin.Price);

        GameDataManager.PurchaseSkin(skinId);
        GameEventsManager.SkinPurchased(skinId, skin.PriceType, skin.Price);
    }

    public static void PutOnSkin(int skinId)
    {
        if (!GameDataManager.PlayerData.PurchasedSkinIds.Contains(skinId))
        {
            Debug.LogWarning("Skin not purchased.");
            return;
        }

        if (CurrentSkin.IsUltaActive.Value)
        {
            Debug.LogWarning("Cannot change skin while Ulta is active.");
            return;
        }

        GameDataManager.PlayerData.AppliedSkinId = skinId;

        if (LevelController.Instance.LevelData.Hamster != null)
        {
            HelpMethods.ApplyOverrideController(LevelController.Instance.LevelData.Hamster);
        }
    }



    private static async Task<string> LoadJsonFromAddressables()
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>("skins");
        await handle.Task;
        return handle.Result.text;
    }

}
