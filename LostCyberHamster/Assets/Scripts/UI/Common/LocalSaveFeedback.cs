using System;
using System.Linq;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает ошибку локальной записи рядом с текущим экраном, сохраняя доступность управления.</summary>
    public sealed class LocalSaveFeedback : MonoBehaviour
    {
        private Label _message;
        private double _visibleSeconds;
        private double _previousUpdate;
        private double _nextLookup;
        private bool _paused;

        private void OnEnable() => GameDataManager.SaveFailed += OnSaveFailed;

        private void OnDisable()
        {
            GameDataManager.SaveFailed -= OnSaveFailed;
            _message?.RemoveFromHierarchy();
            _message = null;
        }

        private void OnSaveFailed(Exception exception)
        {
            _visibleSeconds = 15;
            _previousUpdate = Time.realtimeSinceStartupAsDouble;
            _nextLookup = 0;
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            _previousUpdate = Time.realtimeSinceStartupAsDouble;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) _previousUpdate = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            if (_paused || !Application.isFocused) return;
            var now = Time.realtimeSinceStartupAsDouble;
            _visibleSeconds = Math.Max(0, _visibleSeconds - Math.Min(1, Math.Max(0, now - _previousUpdate)));
            _previousUpdate = now;
            if (_visibleSeconds <= 0)
            {
                _message?.RemoveFromHierarchy();
                _message = null;
                return;
            }
            if (_message?.panel != null || now < _nextLookup) return;
            _nextLookup = now + 0.5;

            // Выбираем игровой UI активной сцены, исключая постоянные служебные панели.
            var scene = SceneManager.GetActiveScene();
            var document = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == scene && item.rootVisualElement?.panel != null)
                .OrderByDescending(item => item.gameObject.name == "[UI]").FirstOrDefault();
            if (document == null) return;
            string text = LocalizationManager.GetLocalizedString("local_save_failed");
            if (string.IsNullOrWhiteSpace(text) || text == "local_save_failed")
                text = "Изменения не сохранены. Повторите действие.";
            _message = new Label(text) { name = "local-save-error", pickingMode = PickingMode.Ignore };
            _message.style.position = Position.Absolute;
            _message.style.left = Length.Percent(15);
            _message.style.right = Length.Percent(15);
            _message.style.top = Length.Percent(3);
            _message.style.paddingLeft = 12;
            _message.style.paddingRight = 12;
            _message.style.paddingTop = 8;
            _message.style.paddingBottom = 8;
            _message.style.backgroundColor = new Color(0.35f, 0.08f, 0.08f, 0.95f);
            _message.style.color = Color.white;
            _message.style.fontSize = 16;
            _message.style.whiteSpace = WhiteSpace.Normal;
            _message.style.unityTextAlign = TextAnchor.MiddleCenter;
            document.rootVisualElement.Add(_message);
        }
    }
}
