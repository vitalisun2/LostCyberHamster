using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Рисует remaining/duration владельца способности без собственного таймера.</summary>
    public sealed class AbilityActivityIndicator : VisualElement
    {
        private readonly Label _seconds;
        private readonly Label _combinations;
        private float _fraction;
        private int _lastSeconds = -1, _lastCombinations = -1;
        private bool _wasFinishing, _wasActive;

        public AbilityActivityIndicator()
        {
            name = "ability-activity";
            pickingMode = PickingMode.Ignore;
            AddToClassList("ability-activity");
            _seconds = new Label { pickingMode = PickingMode.Ignore };
            _seconds.AddToClassList("ability-activity__seconds");
            _combinations = new Label { pickingMode = PickingMode.Ignore };
            _combinations.AddToClassList("ability-activity__combinations");
            Add(_seconds);
            Add(_combinations);
            generateVisualContent += Draw;
            style.display = DisplayStyle.None;
        }

        public void Render(SuperAttackRuntimeSnapshot snapshot)
        {
            bool active = snapshot.IsActive && snapshot.Duration > 0;
            if (active != _wasActive) style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active) { _wasActive = false; return; }
            float fraction = Mathf.Clamp01(snapshot.Remaining / snapshot.Duration);
            if (_fraction != fraction) { _fraction = fraction; MarkDirtyRepaint(); }

            // Текст меняется только на границе секунды, фазы или числа комбинаций.
            int seconds = Mathf.CeilToInt(snapshot.Remaining);
            int combinations = snapshot.AbilityId == 3 ? snapshot.RemainingCombinations : -1;
            if (!_wasActive || seconds != _lastSeconds || snapshot.IsFinishing != _wasFinishing)
            {
                _seconds.text = snapshot.IsFinishing
                    ? LocalizationManager.GetLocalizedString("progression_until_landing")
                    : string.Format(LocalizationManager.GetLocalizedString("progression_seconds"), seconds);
                _seconds.EnableInClassList("ability-activity__seconds--finishing", snapshot.IsFinishing);
            }
            if (!_wasActive || combinations != _lastCombinations)
            {
                _combinations.style.display = combinations >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _combinations.text = combinations >= 0
                    ? string.Format(LocalizationManager.GetLocalizedString("progression_combinations"), combinations) : string.Empty;
            }
            _lastSeconds = seconds;
            _lastCombinations = combinations;
            _wasFinishing = snapshot.IsFinishing;
            _wasActive = true;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var center = contentRect.center;
            float radius = Mathf.Max(0, Mathf.Min(contentRect.width, contentRect.height) * .5f - 7);
            painter.lineWidth = 8;
            painter.strokeColor = new Color(.04f, .10f, .14f, .95f);
            painter.BeginPath();
            painter.Arc(center, radius, -90f, 270f);
            painter.Stroke();
            if (_fraction <= 0) return;
            painter.strokeColor = new Color(.35f, .92f, 1f, 1);
            painter.BeginPath();
            painter.Arc(center, radius, -90f, -90f + 360f * _fraction);
            painter.Stroke();
        }
    }
}
