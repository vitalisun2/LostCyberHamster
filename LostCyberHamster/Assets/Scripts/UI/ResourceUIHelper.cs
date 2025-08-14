using UnityEngine;
using UnityEngine.AddressableAssets;
using Vues.GameCore;

public static class ResourceUIHelper
{
    public static Texture2D GetResourceImage(ResourceType resource)
        {   
            string imageAsset = "watchad"; 
            switch (resource){
                case ResourceType.Coins:
                    imageAsset = "coin";
                    break;
                case ResourceType.Crystals:
                    imageAsset = "crystal";
                    break;
                case ResourceType.Advertisement:
                    imageAsset = "watchad";
                    break;
            }

            var op = Addressables.LoadAssetAsync<Texture2D>(imageAsset);
            op.WaitForCompletion();
            var image = op.Result;
            Addressables.Release(op);
            return image;
        }
}