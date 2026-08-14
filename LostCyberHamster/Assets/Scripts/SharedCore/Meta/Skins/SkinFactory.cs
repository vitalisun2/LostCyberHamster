using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vues.GameCore;

public static class SkinFactory
{
    public static async Task<Skin> CreateSkinAsync(SkinData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return new Skin
        {
            Id = data.Id,
            NameLocalizationKey = data.NameLocalizationKey,
            Price = data.Price,
            PriceType = data.PriceType,
            HamsterSprite = await LoadSpriteAsync(data.SkinSprite),
            SkinVisualAddress = data.SkinVisualAddress,
            SkateboardSkinVisualAddress = data.SkateboardSkinVisualAddress,
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
}
