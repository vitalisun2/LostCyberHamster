using System;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class Energybar : VisualElement
    {
        [SerializeField, DontCreateProperty]
        float _value = 100;

        [SerializeField, DontCreateProperty]
        private VisualTreeAsset _visualTree;

        [UxmlAttribute, CreateProperty]
        public float value
        {
            get => _value;
            set
            {
                _value = value;

                if (value > 100)
                {
                    _value = 100;
                }

                if (value < 0)
                {
                    _value = 0;
                }

                UpdateEnergybar();
            }
        }

        private VisualElement _foreground;

        private void UpdateEnergybar()
        {
            float widthPercentage = _value / 100f;
            _foreground.style.flexGrow = new StyleFloat(widthPercentage);
        }

        public Energybar()
        {
            var op = Addressables.LoadAssetAsync<VisualTreeAsset>("EnergyBar");
            op.WaitForCompletion();
            _visualTree = op.Result;
            Addressables.Release(op);
            this.Add(_visualTree.CloneTree());
            _foreground = this.Q("foreground");
        }
    }
}