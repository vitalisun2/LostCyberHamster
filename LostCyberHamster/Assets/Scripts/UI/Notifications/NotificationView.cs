using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Компактная плашка в safe area; вся иерархия пропускает указатель и не получает фокус.</summary>
    public sealed class NotificationView : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _card;
        private readonly Label _title;
        private readonly Label _detail;
        private NotificationMessage _message;
        private Rect _safeRect;
        private Rect _cardRect;
        private bool _hasLayout;
        private bool _disposed;

        public NotificationView(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _card = new VisualElement { name = "notification-toast", pickingMode = PickingMode.Ignore, focusable = false };
            _title = new Label { name = "notification-title", pickingMode = PickingMode.Ignore, focusable = false };
            _detail = new Label { name = "notification-detail", pickingMode = PickingMode.Ignore, focusable = false };

            // Непрозрачная подложка сохраняет читаемость на движущемся игровом фоне.
            _card.style.position = Position.Absolute;
            _card.style.backgroundColor = new Color(0.045f, 0.075f, 0.12f, 1f);
            _card.style.borderTopLeftRadius = _card.style.borderTopRightRadius = 10;
            _card.style.borderBottomLeftRadius = _card.style.borderBottomRightRadius = 10;
            _card.style.paddingLeft = _card.style.paddingRight = 14;
            _card.style.paddingTop = _card.style.paddingBottom = 10;
            _card.style.borderLeftWidth = 3;
            _card.style.borderLeftColor = new Color(0.3f, 0.85f, 0.95f, 1f);
            _title.style.color = Color.white;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detail.style.color = new Color(0.85f, 0.92f, 0.96f, 1f);
            _title.style.whiteSpace = _detail.style.whiteSpace = WhiteSpace.Normal;
            _title.style.marginTop = _title.style.marginBottom = 0;
            _title.style.marginLeft = _title.style.marginRight = 0;
            _detail.style.marginTop = 3;
            _detail.style.marginBottom = 0;
            _detail.style.marginLeft = _detail.style.marginRight = 0;
            _title.style.flexShrink = _detail.style.flexShrink = 0;
            _card.Add(_title);
            _card.Add(_detail);
            Hide();
            _root.Add(_card);
        }

        /// <summary>Показывает плашку только в свободном прямоугольнике; исключения заданы в координатах panel.</summary>
        public bool TryShow(NotificationMessage message, IReadOnlyList<Rect> excludedRects)
        {
            if (_disposed || _root.panel == null || _card.parent != _root || !IsRootVisible()) return false;
            Rect safe = UiSafeArea.GetLocalRect(_root);
            if (safe.width < 180 || safe.height < 100) return false;

            // Размер пересчитывается при смене текста или геометрии, а не каждый кадр.
            if (!_hasLayout || !ReferenceEquals(_message, message) || safe != _safeRect)
            {
                _message = message;
                _safeRect = safe;
                _hasLayout = true;
                _title.text = message.Title;
                _detail.text = message.Detail;
                _detail.style.display = string.IsNullOrEmpty(message.Detail) ? DisplayStyle.None : DisplayStyle.Flex;
                float width = Mathf.Min(safe.width - 24, Mathf.Max(300, safe.width * 0.32f));
                float titleSize = Mathf.Clamp(safe.width / 55, 18, 28);
                float detailSize = Mathf.Clamp(safe.width / 70, 14, 22);
                _title.style.fontSize = titleSize;
                _detail.style.fontSize = detailSize;
                // Измерение ждёт применения font styles; нулевой первый layout не становится постоянным кешем.
                if (!Mathf.Approximately(_title.resolvedStyle.fontSize, titleSize) ||
                    !string.IsNullOrEmpty(message.Detail) &&
                    !Mathf.Approximately(_detail.resolvedStyle.fontSize, detailSize))
                {
                    _hasLayout = false;
                    return false;
                }
                float textWidth = width - 31;
                float height = 20 + _title.MeasureTextSize(message.Title, textWidth,
                    VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined).y;
                if (!string.IsNullOrEmpty(message.Detail))
                    height += 3 + _detail.MeasureTextSize(message.Detail, textWidth,
                        VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined).y;
                _cardRect = new Rect(safe.xMin + (safe.width - width) / 2, safe.yMin + 8, width, height);
                if (height <= 20)
                {
                    _hasLayout = false;
                    return false;
                }
                _card.style.left = _cardRect.x;
                _card.style.top = _cardRect.y;
                _card.style.width = width;
                _card.style.height = height;
            }

            // Чрезмерно длинный текст или занятый HUD слот откладывает показ, сохраняя само сообщение.
            if (_cardRect.height <= 20 || _cardRect.height > safe.height * 0.22f) return false;
            Rect panelRect = _root.LocalToWorld(_cardRect);
            if (excludedRects != null)
                for (int i = 0; i < excludedRects.Count; i++)
                    if (panelRect.Overlaps(excludedRects[i])) return false;
            _card.BringToFront();
            _card.style.display = DisplayStyle.Flex;
            return true;
        }

        /// <summary>Скрывает плашку без изменения состояния источника.</summary>
        public void Hide()
        {
            if (!_disposed) _card.style.display = DisplayStyle.None;
        }

        private bool IsRootVisible()
        {
            for (var element = _root; element != null; element = element.parent)
                if (element.resolvedStyle.display == DisplayStyle.None ||
                    element.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _card.RemoveFromHierarchy();
        }
    }
}
