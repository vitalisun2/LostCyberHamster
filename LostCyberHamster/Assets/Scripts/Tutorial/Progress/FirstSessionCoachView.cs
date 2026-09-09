using System;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Компактная подсказка меню с обычными кнопками; остальные элементы экрана остаются доступны.</summary>
    internal sealed class FirstSessionCoachView : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _card;
        private readonly Label _title;
        private readonly Label _detail;
        private readonly Button _primary;
        private readonly Button _secondary;
        private Action _primaryAction;
        private Action _secondaryAction;

        public FirstSessionCoachView(VisualElement root)
        {
            _root = root;
            _card = new VisualElement { name = "first-session-coach", pickingMode = PickingMode.Ignore };
            _card.style.position = Position.Absolute;
            _card.style.backgroundColor = new Color(.045f, .075f, .12f, 1f);
            _card.style.paddingLeft = _card.style.paddingRight = 12;
            _card.style.paddingTop = _card.style.paddingBottom = 8;
            _card.style.borderTopLeftRadius = _card.style.borderTopRightRadius = 10;
            _card.style.borderBottomLeftRadius = _card.style.borderBottomRightRadius = 10;
            _title = new Label { pickingMode = PickingMode.Ignore };
            _detail = new Label { pickingMode = PickingMode.Ignore };
            _title.style.color = Color.white;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detail.style.color = new Color(.85f, .92f, .96f);
            _title.style.whiteSpace = _detail.style.whiteSpace = WhiteSpace.Normal;
            var actions = new VisualElement { pickingMode = PickingMode.Ignore };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            _primary = new Button(() => _primaryAction?.Invoke());
            _secondary = new Button(() => _secondaryAction?.Invoke());
            _primary.style.minHeight = _secondary.style.minHeight = 36;
            actions.Add(_primary);
            actions.Add(_secondary);
            _card.Add(_title);
            _card.Add(_detail);
            _card.Add(actions);
            _root.Add(_card);
            Hide();
        }

        public bool Show(string title, string detail, string primary, Action onPrimary,
            string secondary = null, Action onSecondary = null, bool goal = false, Rect? target = null)
        {
            if (_root.panel == null || _card.parent != _root) return false;
            Rect safe = UiSafeArea.GetLocalRect(_root);
            if (safe.width < 300 || safe.height < 180) return false;

            // Домашняя цель располагается над нижней навигацией; урок использует верхний центральный слот.
            float width = Mathf.Min(safe.width - 24, Mathf.Max(300, safe.width * .36f));
            _card.style.width = width;
            _card.style.left = goal ? safe.xMin + 12 : safe.center.x - width / 2;
            float top = safe.yMin + safe.height * (goal ? .48f : .11f);
            if (!goal && target.HasValue && target.Value.Overlaps(new Rect(safe.center.x - width / 2, top, width, 150)))
                top = safe.yMin + safe.height * .66f;
            _card.style.top = top;
            _title.style.fontSize = Mathf.Clamp(safe.width / 65, 16, 24);
            _detail.style.fontSize = Mathf.Clamp(safe.width / 80, 14, 20);
            _primary.style.fontSize = _secondary.style.fontSize = Mathf.Clamp(safe.width / 80, 14, 20);

            // Назначения кнопок обновляются вместе с текстом, без накопления подписок.
            _title.text = title;
            _detail.text = detail ?? string.Empty;
            _detail.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
            _primary.text = primary;
            _primary.style.display = string.IsNullOrEmpty(primary) ? DisplayStyle.None : DisplayStyle.Flex;
            _secondary.text = secondary;
            _secondary.style.display = string.IsNullOrEmpty(secondary) ? DisplayStyle.None : DisplayStyle.Flex;
            _primaryAction = onPrimary;
            _secondaryAction = onSecondary;
            _card.style.display = DisplayStyle.Flex;
            _card.BringToFront();
            return true;
        }

        public void Hide() => _card.style.display = DisplayStyle.None;
        public void Dispose() => _card.RemoveFromHierarchy();
    }
}
