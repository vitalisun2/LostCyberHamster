using UnityEngine.AddressableAssets;

namespace Extensions
{
    public static class AddressableExtentions
    {
        public static T LoadAssetSync<T>(string key)
        {
            var op = Addressables.LoadAssetAsync<T>(key);
            op.WaitForCompletion();
            var asset = op.Result;
            Addressables.Release(op);
            return asset;
        }
    }
}
