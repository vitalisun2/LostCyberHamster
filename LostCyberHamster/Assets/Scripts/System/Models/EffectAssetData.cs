namespace Assets.Scripts.System.Models
{
    public class EffectAssetData
    {
        public string PrefabName { get; set; }

        public string SpriteName { get; set; }

        public string SortingLayer { get; set; }

        public EffectAssetData(string prefabName, string spriteName, string sortingLayer)
        {
            PrefabName = prefabName;
            SpriteName = spriteName;
            SortingLayer = sortingLayer;
        }
    }
}
