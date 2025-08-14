using System;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
            _label.text = MoneyStorage.GetCurrentBalance().ToString();
        }
    }
}