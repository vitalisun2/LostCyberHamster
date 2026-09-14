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
        private const float LowEnergyThreshold = MaxValue * 0.2f;
        private const float LowEnergyMinOpacity = 0.35f;
        private const float LowEnergyPulseSpeed = 3f;
        private const int LowEnergyPulseIntervalMs = 60;

        [SerializeField, DontCreateProperty]
        float _value = MaxValue;

        [SerializeField, DontCreateProperty]
        private VisualTreeAsset _visualTree;

        private IVisualElementScheduledItem _lowEnergyPulseSchedule;

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

            UpdateLowEnergyPulseState();
        }

        private void UpdateLowEnergyPulseState()
        {
            bool shouldPulse = _value > MinValue && _value <= LowEnergyThreshold;
            if (!shouldPulse)
            {
                _lowEnergyPulseSchedule?.Pause();
                style.opacity = 1f;
                return;
            }

            if (panel == null)
            {
                return;
            }

            _lowEnergyPulseSchedule?.Resume();
            TickLowEnergyPulse();
        }

        private void TickLowEnergyPulse()
        {
            float pulse = Mathf.PingPong(Time.realtimeSinceStartup * LowEnergyPulseSpeed, 1f);
            style.opacity = Mathf.Lerp(LowEnergyMinOpacity, 1f, pulse);
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            UpdateLowEnergyPulseState();
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            _lowEnergyPulseSchedule?.Pause();
            style.opacity = 1f;
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
            _lowEnergyPulseSchedule = schedule.Execute(TickLowEnergyPulse).Every(LowEnergyPulseIntervalMs);
            _lowEnergyPulseSchedule.Pause();
            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            ApplyValueToView();
        }
    }
}
