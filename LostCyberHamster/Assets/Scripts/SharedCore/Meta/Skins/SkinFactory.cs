using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vues.GameCore;

public static class SkinFactory
{
    private static readonly Dictionary<int, Func<Skin>> SkinCreators = new()
    {
        { 0, () => new DefaultSkin() },
        { 1, () => new EnergyShieldSkin() },
        { 2, () => new ElectricStrikeSkin() },
    };

    public static async Task<Skin> CreateSkinAsync(SkinData data = null)
    {
        Skin skin;

        if (data == null)
        {
            skin = new DefaultSkin();

            // Initialize common properties
            skin.Id = 0;
            skin.NameLocalizationKey = "default";
            skin.Price = 0;
            skin.PriceType = ResourceType.Crystals;
            skin.HamsterOverrideController = await LoadAnimatorControllerAsync(data.HamsterOverrideController);
            skin.UltaDuration = 0;
            skin.UltaCharge = 0;

            return skin;
        }

        if (SkinCreators.TryGetValue(data.Id, out var createSkin))
        {
            skin = createSkin();

            // Initialize common properties
            skin.Id = data.Id;
            skin.NameLocalizationKey = data.NameLocalizationKey;
            skin.Price = data.Price;
            skin.PriceType = data.PriceType;
            skin.HamsterSprite = await LoadSpriteAsync(data.SkinSprite);
            skin.HamsterOverrideController = await LoadAnimatorControllerAsync(data.HamsterOverrideController);
            skin.UltaPrefab = await LoadPrefabAsync(data.UltaPrefab);
            skin.UltaDuration = data.UltaDuration;
            skin.UltaCharge = data.UltaCharge;

            return skin;
        }
        throw new Exception($"Skin with Id {data.Id} not found.");
    }



    private static async Task<Sprite> LoadSpriteAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("HamsterSprite path is empty.");
            return null;
        }

        return await Addressables.LoadAssetAsync<Sprite>(path).Task;
    }

    private static async Task<GameObject> LoadPrefabAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("UltaPrefab path is empty.");
            return null;
        }

        return await Addressables.LoadAssetAsync<GameObject>(path).Task;
    }

    private static async Task<RuntimeAnimatorController> LoadAnimatorControllerAsync(string path)
    {
        if(string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("HamsterOverrideController path is empty.");
            return null;
        }

        var controller = await Addressables.LoadAssetAsync<RuntimeAnimatorController>(path).Task;

        return controller;
    }
}
