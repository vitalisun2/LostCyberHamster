using System;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class Healthbar : VisualElement
    {
        [SerializeField, DontCreateProperty]
        int _value = 3;

        [SerializeField, DontCreateProperty]
        private VisualTreeAsset _visualTree;

        [UxmlAttribute, CreateProperty]
        public int value
        {
            get => _value;
            set
            {
                _value = value;

                if (value > 3)
                {
                    _value = 3;
                }

                if (value < 0)
                {
                    _value = 0;
                }

                UpdateHealthbar();
            }
        }

        private void UpdateHealthbar()
        {
            this.Clear();

            for (int i = 0; i < _value; i++)
            {
                _visualTree?.CloneTree(this);
            }
        }

        public Healthbar()
        {
            var op = Addressables.LoadAssetAsync<VisualTreeAsset>("HealthItem");
            op.WaitForCompletion();
            _visualTree = op.Result;
            Addressables.Release(op);
            this.Add(_visualTree.CloneTree());
            value = 3;
        }
    }
}