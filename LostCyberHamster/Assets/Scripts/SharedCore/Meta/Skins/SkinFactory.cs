using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vues.GameCore;

public static class SkinFactory
{
    private static readonly HashSet<int> SupportedSkinIds = new()
    {
        0,
        1,
        2,
    };

    public static async Task<Skin> CreateSkinAsync(SkinData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (!SupportedSkinIds.Contains(data.Id))
        {
            throw new KeyNotFoundException($"Skin with Id {data.Id} not found.");
        }

        return new Skin
        {
            Id = data.Id,
            NameLocalizationKey = data.NameLocalizationKey,
            Price = data.Price,
            PriceType = data.PriceType,
            HamsterSprite = await LoadSpriteAsync(data.SkinSprite),
            HamsterOverrideController = await LoadAnimatorControllerAsync(data.HamsterOverrideController),
        };
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

    private static async Task<RuntimeAnimatorController> LoadAnimatorControllerAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("HamsterOverrideController path is empty.");
            return null;
        }

        var controller = await Addressables.LoadAssetAsync<RuntimeAnimatorController>(path).Task;

        return controller;
    }
}
