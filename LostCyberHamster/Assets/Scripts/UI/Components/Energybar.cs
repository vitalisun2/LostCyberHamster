using Unity.Properties;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    [UxmlElement]
    public partial class Energybar : VisualElement
    {
        private const float MinValue = 0f;
        private const float MaxValue = 100f;

        [SerializeField, DontCreateProperty]
        float _value = MaxValue;

        [SerializeField, DontCreateProperty]
        private VisualTreeAsset _visualTree;

        [UxmlAttribute, CreateProperty]
        public float value
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp(value, MinValue, MaxValue);
                ApplyValueToView();
            }
        }

        private VisualElement _foreground;
        private Label _valueLabel;

        private void ApplyValueToView()
        {
            if (_foreground == null)
            {
                return;
            }

            // Обновляем видимую долю шкалы.
            float fillPercentage = _value / MaxValue * 100f;
            _foreground.style.flexGrow = new StyleFloat(0f);
            _foreground.style.width = new StyleLength(Length.Percent(fillPercentage));

            // Показываем текущее значение поверх шкалы.
            if (_valueLabel != null)
            {
                _valueLabel.text = $"{Mathf.RoundToInt(_value)} / {Mathf.RoundToInt(MaxValue)}";
            }
        }

        public Energybar()
        {
            var op = Addressables.LoadAssetAsync<VisualTreeAsset>("EnergyBar");
            op.WaitForCompletion();
            _visualTree = op.Result;
            Addressables.Release(op);
            this.Add(_visualTree.CloneTree());
            _foreground = this.Q("foreground");
            _valueLabel = this.Q<Label>("energy-value");
            ApplyValueToView();
        }
    }
}
