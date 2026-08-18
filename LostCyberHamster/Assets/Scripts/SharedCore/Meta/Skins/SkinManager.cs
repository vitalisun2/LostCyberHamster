using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vues.GameCore;
using Vues.GameCore.Quests;

public static class SkinManager
{
    public static List<string> AvailableSkinsNames => _availableSkins.Select(x => x.Name).ToList();
    public static string CurrentSkinName => CurrentSkin?.Name;

    public static List<Skin> AvailableSkins => _availableSkins;

    public static Skin CurrentSkin => _availableSkins.FirstOrDefault(x => x.Id == GameDataManager.PlayerData.AppliedSkinId);
    public static Skin DefaultSkin => _availableSkins.FirstOrDefault(x => x.Id == 0);

    private static List<Skin> _availableSkins = new();

    public static async Task Init()
    {
        _availableSkins.Clear();
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
        return skin != null &&
               CharacterDevelopmentService.IsSkinUnlocked(skinId) &&
               !GameDataManager.PlayerData.PurchasedSkinIds.Contains(skinId) &&
               ResourceManager.CanSpendResource(skin.PriceType, skin.Price);
    }

    /// <summary>
    /// Покупает доступный скин и сохраняет новое состояние игрока.
    /// </summary>
    public static void PurchaseSkin(int skinId)
    {
        var skin = _availableSkins.FirstOrDefault(x => x.Id == skinId);

        if (skin == null ||
            !CharacterDevelopmentService.IsSkinUnlocked(skinId) ||
            GameDataManager.PlayerData.PurchasedSkinIds.Contains(skinId))
        {
            return;
        }

        if (!ResourceManager.SpendResource(skin.PriceType, skin.Price))
        {
            Debug.LogWarning("Not enough resources to purchase skin.");
            return;
        }

        GameDataManager.PlayerData.PurchasedSkinIds.Add(skinId);
        GameEventsManager.SkinPurchased(skinId, skin.PriceType, skin.Price);
        GameEventsManager.PlayerStateChanged(
            PlayerStateIds.SkinOwned,
            skinId.ToString(CultureInfo.InvariantCulture));
        PlayerProgressCommitter.Commit(CheckpointReason.SkinPurchased);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Возвращает скин в состояние до покупки для повторной проверки через dev tool.
    /// </summary>
    public static bool ResetSkinPurchaseForTesting(int skinId)
    {
        if (skinId <= 0 || _availableSkins.All(skin => skin.Id != skinId))
        {
            return false;
        }

        // Удаляем владение целевым скином.
        GameDataManager.PlayerData.PurchasedSkinIds ??= new List<int>();
        GameDataManager.PlayerData.PurchasedSkinIds.RemoveAll(
            purchasedSkinId => purchasedSkinId == skinId);

        // Возвращаем безопасный default, если целевой скин был надет.
        if (GameDataManager.PlayerData.AppliedSkinId == skinId)
        {
            GameDataManager.PlayerData.AppliedSkinId = 0;
        }

        return true;
    }
#endif

    /// <summary>
    /// Применяет принадлежащий игроку скин и сохраняет выбор.
    /// </summary>
    public static void PutOnSkin(int skinId)
    {
        var skin = _availableSkins.FirstOrDefault(x => x.Id == skinId);
        if (skin == null || !GameDataManager.PlayerData.PurchasedSkinIds.Contains(skinId))
        {
            Debug.LogWarning("Skin not purchased.");
            return;
        }

        GameDataManager.PlayerData.AppliedSkinId = skinId;

        GameEventsManager.PlayerStateChanged(
            PlayerStateIds.SkinApplied,
            skinId.ToString(CultureInfo.InvariantCulture));
        PlayerProgressCommitter.Commit(CheckpointReason.SkinApplied);
    }



    private static async Task<string> LoadJsonFromAddressables()
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>("skins");
        await handle.Task;
        return handle.Result.text;
    }

}
