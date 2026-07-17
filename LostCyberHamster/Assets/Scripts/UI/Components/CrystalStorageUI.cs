using Unity.Properties;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class CrystalStorageUI : BaseStorageUI
    {
        public CrystalStorageUI()
        {            
        }

        protected override string _imageAssetName => "crystal";

        protected override void UpdateText()
        {
            _label.text = ResourceManager.GetCurrentBalance(ResourceType.Crystals).ToString();
        }

    }
}
