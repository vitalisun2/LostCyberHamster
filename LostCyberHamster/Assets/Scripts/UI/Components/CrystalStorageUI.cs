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
    public partial class CrystalStorageUI : BaseStorageUI
    {
        public CrystalStorageUI()
        {            
        }

        protected override string _imageAssetName => "crystal";

        protected override void UpdateText()
        {
            _label.text = CrystalStorage.GetCurrentBalance().ToString();
        }

    }
}