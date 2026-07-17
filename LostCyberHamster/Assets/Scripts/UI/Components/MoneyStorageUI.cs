using Unity.Properties;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class MoneyStorageUI : BaseStorageUI
    {
        protected override string _imageAssetName => "coin";
        public MoneyStorageUI()
        {
        }

        protected override void UpdateText()
        {
            _label.text = ResourceManager.GetCurrentBalance(ResourceType.Coins).ToString();
        }
    }
}
