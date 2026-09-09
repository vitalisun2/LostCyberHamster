using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Отображает локализованную цель. Владелец повторно проверяет доступность действия.</summary>
    public sealed class NextGoalCardView : IDisposable
    {
        private const float ArtboardWidth = 1672f;
        private const float ArtboardHeight = 941f;
        private readonly VisualElement _host;
        private readonly VisualElement _card;
        private readonly VisualElement _icon;
        private readonly VisualElement _progress;
        private readonly VisualElement _progressFill;
        private readonly Label _heading;
        private readonly Label _text;
        private readonly Label _detail;
        private readonly Button _primary;
        private readonly Button _dismiss;
        private readonly List<VisualElement> _observed = new List<VisualElement>(6);
        private readonly VisualElement _homeSelect;
        private readonly VisualElement _homeNavigation;
        private readonly VisualElement _homeActivities;
        private readonly VisualElement _winPanel;
        private readonly VisualElement _timePanel;
        private readonly VisualElement _levelPanel;
        private NextGoalCardPlacement _placement;
        private Action _click;
        private Action _close;
        private bool _shown;
        private bool _disposed;
        private bool _visible;
        private Rect _lastRect;
        private float _lastScale = -1f;

        public bool IsVisible => !_disposed && _shown && _visible && _host.panel != null &&
            _card.parent == _host && Visible(_card);

        public NextGoalCardView(VisualElement host)
        {
            // Сохраняем опорные элементы уже построенного экрана.
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _homeSelect = host.Q("btn_select-level");
            _homeNavigation = host.Q(className: "home-navigation");
            _homeActivities = host.Q("home-activities");
            _winPanel = host.Q(className: "game-result-modal__panel--win");
            _timePanel = host.Q(className: "select-time-panel");
            _levelPanel = host.Q(className: "select-level-panel");

            // Создаём независимую карточку; художественное оформление задаёт общий USS.
            _card = Element("next-goal-card", "next-goal-card");
            _card.style.visibility = Visibility.Hidden;
            var header = Element(null, "next-goal-card__header");
            _icon = Element(null, "next-goal-card__icon");
            _heading = Text("next-goal-card__heading");
            _text = Text("next-goal-card__text");
            _detail = Text("next-goal-card__detail");
            _progress = Element(null, "next-goal-card__progress");
            _progressFill = Element(null, "next-goal-card__progress-fill");
            _primary = new Button { name = "next-goal-card-action" };
            _primary.AddToClassList("next-goal-card__action");
            _dismiss = new Button { name = "next-goal-card-dismiss", text = "×" };
            _dismiss.AddToClassList("next-goal-card__dismiss");
            header.Add(_heading);
            _card.Add(header);
            _card.Add(_icon);
            _card.Add(_text);
            _progress.Add(_progressFill);
            _card.Add(_progress);
            _card.Add(_detail);
            _card.Add(_primary);
            _card.Add(_dismiss);
        }

        /// <remarks>Требует components/next-goal-card.uss в таблицах стилей родителя.
        /// False означает незавершённую вёрстку или недостаток места. RefreshLayout повторяет расчёт без таймеров.
        /// Sprite или Texture2D передаются через new StyleBackground(icon); сроком жизни ассета управляет владелец.</remarks>
        public bool Show(string text, string detail, string actionText, StyleBackground icon,
            Action click, Action dismiss, NextGoalCardPlacement placement, string heading = null,
            float? progress = null)
        {
            if (_disposed) return false;
            if (string.IsNullOrWhiteSpace(text)) { Hide(); return false; }
            if (_shown && _placement != placement) Hide();

            // Обновляем текст и действие вместе, без собственной логики выбора цели.
            _placement = placement;
            _click = click;
            _close = dismiss;
            _text.text = text;
            _detail.text = detail ?? string.Empty;
            _heading.text = heading ?? string.Empty;
            _primary.text = actionText ?? string.Empty;
            _primary.style.display = click != null && !string.IsNullOrWhiteSpace(actionText)
                ? DisplayStyle.Flex : DisplayStyle.None;
            _dismiss.style.display = dismiss != null ? DisplayStyle.Flex : DisplayStyle.None;
            _icon.style.backgroundImage = icon;
            _progress.style.display = progress.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            _progressFill.style.width = Length.Percent(progress.HasValue && !float.IsNaN(progress.Value)
                ? Mathf.Clamp01(progress.Value) * 100f : 0f);
            _card.EnableInClassList("next-goal-card--home", placement == NextGoalCardPlacement.Home);
            _card.EnableInClassList("next-goal-card--progress", progress.HasValue);

            // На одно открытие приходится один комплект подписок.
            if (!_shown)
            {
                _shown = true;
                _primary.clicked += OnPrimary;
                _dismiss.clicked += OnDismiss;
                _host.Add(_card);
                Observe(_host);
                Observe(_host.Q("select-level-design"));
                Observe(_host.Q(className: "game-result-modal__design"));
                Observe(_homeSelect);
                Observe(_homeNavigation);
                Observe(_homeActivities);
            }
            return RefreshLayout();
        }

        public bool RefreshLayout()
        {
            if (_disposed || !_shown) return false;
            if (_host.panel == null || _card.parent != _host) return SetVisible(false);
            Rect safe = UiSafeArea.GetLocalRect(_host);
            if (!Usable(safe)) return SetVisible(false);
            bool home = _placement == NextGoalCardPlacement.Home;
            float width = home ? 480f : 360f;
            float height = home ? 188f : 330f;
            float scale;
            Rect slot;
            if (home)
            {
                // Домашняя цель занимает только правый промежуток над нижней навигацией.
                var select = _homeSelect;
                var navigation = _homeNavigation;
                if (!Visible(select) || !Visible(navigation)) return SetVisible(false);
                Rect selectRect = LocalRect(select);
                Rect navRect = LocalRect(navigation);
                float basis = Mathf.Min(safe.width / ArtboardWidth, safe.height / ArtboardHeight);
                float gap = 10f * basis;
                float left = safe.center.x + gap;
                var activities = _homeActivities;
                if (Visible(activities)) left = Mathf.Max(left, LocalRect(activities).xMax + gap);
                slot = Rect.MinMaxRect(left, selectRect.yMax + gap, safe.xMax - gap,
                    Mathf.Min(safe.yMax, navRect.yMin) - gap);
                if (!Usable(slot)) return SetVisible(false);
                scale = Mathf.Min(basis, Mathf.Min(slot.width / width, slot.height / height));
                if (scale < basis * .7f) return SetVisible(false);
                float x = Mathf.Clamp(selectRect.center.x - width * scale / 2f,
                    slot.xMin, slot.xMax - width * scale);
                slot = new Rect(x, slot.yMin, width * scale, height * scale);
            }
            else
            {
                // Боковая цель следует масштабу центральной панели, сохраняя свободный зазор.
                VisualElement panel = _placement == NextGoalCardPlacement.Win
                    ? _winPanel : _timePanel;
                float panelWidth = _placement == NextGoalCardPlacement.Win ? 782f : 839f;
                if (_placement == NextGoalCardPlacement.SelectLevel && !Visible(panel))
                {
                    panel = _levelPanel;
                    panelWidth = 748f;
                }
                if (!Visible(panel)) return SetVisible(false);
                Rect panelRect = LocalRect(panel);
                float basis = panelRect.width / panelWidth;
                float gap = 24f * basis;
                slot = Rect.MinMaxRect(Mathf.Max(safe.xMin, panelRect.xMax + gap),
                    safe.yMin + 140f * basis, safe.xMax - gap, safe.yMax - gap);
                if (!Usable(slot)) return SetVisible(false);
                scale = Mathf.Min(basis, Mathf.Min(slot.width / width, slot.height / height));
                if (scale < basis * .8f) return SetVisible(false);
                float y = Mathf.Clamp(panelRect.center.y - height * scale / 2f,
                    slot.yMin, slot.yMax - height * scale);
                slot = new Rect(slot.xMin, y, width * scale, height * scale);
            }

            // Повторный Tick без изменения геометрии не запускает перерасчёт стилей.
            if (_lastRect != slot || _lastScale != scale)
            {
                _lastRect = slot;
                _lastScale = scale;
                _card.style.left = slot.x;
                _card.style.top = slot.y;
                _card.style.width = width;
                _card.style.height = height;
                _card.style.scale = new Scale(new Vector3(scale, scale, 1f));
            }
            return SetVisible(true);
        }

        public void Hide()
        {
            // Снимаем действия и наблюдение за экраном до удаления дерева.
            _shown = false;
            _click = null;
            _close = null;
            _primary.clicked -= OnPrimary;
            _dismiss.clicked -= OnDismiss;
            foreach (var element in _observed)
                element.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _observed.Clear();

            // Освобождаем визуальный слот и ссылку на иконку владельца.
            SetVisible(false);
            _lastScale = -1f;
            _card.RemoveFromHierarchy();
            _icon.style.backgroundImage = StyleKeyword.None;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Hide();
            _disposed = true;
        }

        private void OnPrimary() { var action = _click; Hide(); action?.Invoke(); }
        private void OnDismiss() { var action = _close; Hide(); action?.Invoke(); }
        private void OnGeometryChanged(GeometryChangedEvent evt) => RefreshLayout();
        private bool SetVisible(bool visible)
        {
            if (_visible != visible)
            {
                _visible = visible;
                _card.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            }
            return visible;
        }
        private void Observe(VisualElement element)
        {
            if (element == null || _observed.Contains(element)) return;
            _observed.Add(element);
            element.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
        private Rect LocalRect(VisualElement element)
        {
            Rect world = element.worldBound;
            Vector2 min = _host.WorldToLocal(world.min);
            Vector2 max = _host.WorldToLocal(world.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private bool Visible(VisualElement element)
        {
            if (element == null || element.panel != _host.panel || !Usable(element.worldBound)) return false;
            for (var current = element; current != null; current = current.parent)
                if (current.resolvedStyle.display == DisplayStyle.None ||
                    current.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }
        private static bool Usable(Rect rect) => rect.width > 0 && rect.height > 0 &&
            !float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsInfinity(rect.width) && !float.IsInfinity(rect.height);
        private static VisualElement Element(string name, string className)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }
        private static Label Text(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
