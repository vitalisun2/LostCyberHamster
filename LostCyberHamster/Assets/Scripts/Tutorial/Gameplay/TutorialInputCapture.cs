using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Перехватывает учебный ввод, оставляя доступной реальную кнопку паузы.</summary>
    internal sealed class TutorialInputCapture : VisualElement
    {
        private readonly VisualElement _pauseButton;

        public TutorialInputCapture(VisualElement pauseButton)
        {
            _pauseButton = pauseButton;
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            return base.ContainsPoint(localPoint) &&
                   !(_pauseButton?.panel != null && _pauseButton.visible &&
                     _pauseButton.worldBound.Contains(this.LocalToWorld(localPoint)));
        }
    }
}
