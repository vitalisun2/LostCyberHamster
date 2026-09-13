#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.DevTools.UiToolkit
{
    public interface IDevToolsUiPage : IDisposable
    {
        string Title { get; }
        VisualElement Root { get; }
        void OnShown();
        void OnHidden();
        void Refresh();
    }

    public static class DevToolsUiToolkitTheme
    {
        public static readonly Color LauncherBackground = new(1f, 1f, 1f, 0.92f);
        public static readonly Color LauncherDanger = new(1f, 0.78f, 0.74f, 0.98f);
        public static readonly Color Surface = new(0.96f, 0.97f, 1f, 1f);
        public static readonly Color SurfaceAccent = new(0.91f, 0.95f, 1f, 1f);
        public static readonly Color SurfaceDanger = new(1f, 0.93f, 0.92f, 1f);
        public static readonly Color TextStrong = new(0.06f, 0.07f, 0.09f, 1f);
        public static readonly Color TextMuted = new(0.22f, 0.25f, 0.31f, 1f);
        public static readonly Color Border = new(0.84f, 0.89f, 0.97f, 1f);
        public static readonly Color Summary = new(0.90f, 0.95f, 1f, 1f);

        public const float RootPadding = 24f;
        public const float HeaderHeight = 76f;
        public const float ActionHeight = 72f;
        public const float CompactActionHeight = 56f;
        public const float CardSpacing = 18f;
        public const float CardPadding = 24f;
        public const float ActionButtonWidth = 280f;
        public const float ActionRowSpacing = 14f;
        public const float DashboardTileMinWidth = 360f;
        public const float DashboardTileMaxWidth = 640f;
        public const float DashboardTileMinHeight = 164f;
        public const float ContentWidthPortrait = 860f;
        public const float ContentWidthLandscape = 1440f;

        public static float GetTitleSize(bool pageTitle = true)
        {
            return pageTitle ? 44f : 30f;
        }

        public static float GetBodySize()
        {
            return 22f;
        }

        public static float GetCaptionSize()
        {
            return 17f;
        }

        public static float ResolveContentWidth(Rect rect)
        {
            bool landscape = rect.width >= rect.height;
            float maxWidth = landscape ? ContentWidthLandscape : ContentWidthPortrait;
            return Mathf.Clamp(rect.width - RootPadding * 2f, 320f, maxWidth);
        }

        public static void ApplyText(
            TextElement text,
            float fontSize,
            FontStyle fontStyle = FontStyle.Normal,
            bool center = false)
        {
            text.style.fontSize = fontSize;
            text.style.unityFontStyleAndWeight = fontStyle;
            text.style.color = TextStrong;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.unityTextAlign = center
                ? TextAnchor.MiddleCenter
                : TextAnchor.UpperLeft;
        }

        public static void ApplyCard(VisualElement card, Color? background = null)
        {
            card.style.backgroundColor = background ?? Surface;
            card.style.borderTopLeftRadius = 20f;
            card.style.borderTopRightRadius = 20f;
            card.style.borderBottomLeftRadius = 20f;
            card.style.borderBottomRightRadius = 20f;
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderTopColor = Border;
            card.style.borderRightColor = Border;
            card.style.borderBottomColor = Border;
            card.style.borderLeftColor = Border;
            card.style.paddingTop = CardPadding;
            card.style.paddingRight = CardPadding;
            card.style.paddingBottom = CardPadding;
            card.style.paddingLeft = CardPadding;
            card.style.marginBottom = CardSpacing;
            card.style.flexDirection = FlexDirection.Column;
        }

        public static void ApplyActionButton(Button button, Color background, bool compact = false)
        {
            button.style.height = compact ? CompactActionHeight : ActionHeight;
            button.style.backgroundColor = background;
            button.style.borderTopLeftRadius = 16f;
            button.style.borderTopRightRadius = 16f;
            button.style.borderBottomLeftRadius = 16f;
            button.style.borderBottomRightRadius = 16f;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingRight = 18f;
            button.style.paddingBottom = 0f;
            button.style.paddingLeft = 18f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.color = TextStrong;
            button.style.fontSize = compact ? 22f : 25f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.whiteSpace = WhiteSpace.Normal;
        }
    }

    public sealed class DevToolsUiToolkitFactory
    {
        public Label CreateTitle(string text, bool pageTitle = true)
        {
            var label = new Label(text);
            DevToolsUiToolkitTheme.ApplyText(
                label,
                DevToolsUiToolkitTheme.GetTitleSize(pageTitle),
                FontStyle.Bold);
            return label;
        }

        public Label CreateBody(string text)
        {
            var label = new Label(text);
            DevToolsUiToolkitTheme.ApplyText(
                label,
                DevToolsUiToolkitTheme.GetBodySize());
            label.style.color = DevToolsUiToolkitTheme.TextMuted;
            return label;
        }

        public Label CreateStatus(string text)
        {
            var label = CreateBody(text);
            label.style.color = DevToolsUiToolkitTheme.TextStrong;
            return label;
        }

        public Label CreateCaption(string text)
        {
            var label = new Label(text);
            DevToolsUiToolkitTheme.ApplyText(
                label,
                DevToolsUiToolkitTheme.GetCaptionSize());
            label.style.color = DevToolsUiToolkitTheme.TextMuted;
            return label;
        }

        public VisualElement CreateCard(string title, string description = null, Color? background = null)
        {
            var card = new VisualElement();
            DevToolsUiToolkitTheme.ApplyCard(card, background);
            card.Add(CreateTitle(title, pageTitle: false));
            if (!string.IsNullOrWhiteSpace(description))
                card.Add(CreateBody(description));
            return card;
        }

        public Button CreateActionButton(string title, Color background, Action action, bool compact = false)
        {
            var button = new Button(() => action?.Invoke())
            {
                text = title
            };
            DevToolsUiToolkitTheme.ApplyActionButton(button, background, compact);
            return button;
        }

        public VisualElement CreateRow(bool wrap = false)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.flexWrap = wrap ? Wrap.Wrap : Wrap.NoWrap;
            return row;
        }

        public VisualElement CreateSpacer(float height)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            spacer.style.flexShrink = 0f;
            return spacer;
        }

        public IntegerField CreateIntegerField(string label)
        {
            var field = new IntegerField(label);
            field.style.minHeight = 56f;
            field.style.fontSize = 22f;
            field.style.unityFontStyleAndWeight = FontStyle.Bold;
            field.labelElement.style.minWidth = 180f;
            field.labelElement.style.fontSize = 18f;
            field.labelElement.style.color = DevToolsUiToolkitTheme.TextMuted;
            return field;
        }

        public TextField CreateTextField(string label)
        {
            var field = new TextField(label);
            field.style.minHeight = 56f;
            field.style.fontSize = 22f;
            field.labelElement.style.minWidth = 180f;
            field.labelElement.style.fontSize = 18f;
            field.labelElement.style.color = DevToolsUiToolkitTheme.TextMuted;
            return field;
        }

        public DropdownField CreateDropdown(string label)
        {
            var field = new DropdownField(label);
            field.style.minHeight = 56f;
            field.style.fontSize = 20f;
            field.labelElement.style.minWidth = 180f;
            field.labelElement.style.fontSize = 18f;
            field.labelElement.style.color = DevToolsUiToolkitTheme.TextMuted;
            return field;
        }
    }

    public sealed class DevToolsUiToolkitHost : IDisposable
    {
        private readonly bool _showLauncher;
        private readonly bool _useSafeArea;
        private readonly Dictionary<string, IDevToolsUiPage> _pages = new();
        private readonly DevToolsUiToolkitFactory _factory = new();
        private readonly VisualElement _root;
        private readonly Button _launcherButton;
        private readonly VisualElement _panel;
        private readonly VisualElement _pageViewport;
        private readonly VisualElement _pageColumn;
        private readonly Button _backButton;
        private readonly Label _titleLabel;
        private readonly Button _closeButton;
        private VisualElement _attachTarget;
        private string _rootPageId;
        private string _currentPageId;

        public DevToolsUiToolkitHost(bool showLauncher, bool useSafeArea)
        {
            _showLauncher = showLauncher;
            _useSafeArea = useSafeArea;

            _root = new VisualElement { name = "devtools-uitk-root" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0f;
            _root.style.top = 0f;
            _root.style.right = 0f;
            _root.style.bottom = 0f;
            _root.style.flexDirection = FlexDirection.Column;
            _root.pickingMode = PickingMode.Ignore;
            _root.RegisterCallback<GeometryChangedEvent>(_ => RefreshLayout());

            _launcherButton = _factory.CreateActionButton(
                "DEV",
                DevToolsUiToolkitTheme.LauncherBackground,
                Open,
                compact: true);
            _launcherButton.name = "dev-overlay-launcher";
            _launcherButton.style.position = Position.Absolute;
            _launcherButton.style.width = 180f;
            _launcherButton.style.display = showLauncher ? DisplayStyle.Flex : DisplayStyle.None;
            _root.Add(_launcherButton);

            _panel = new VisualElement { name = "dev-overlay-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 0f;
            _panel.style.top = 0f;
            _panel.style.right = 0f;
            _panel.style.bottom = 0f;
            _panel.style.backgroundColor = Color.white;
            _panel.style.display = showLauncher ? DisplayStyle.None : DisplayStyle.Flex;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.paddingTop = DevToolsUiToolkitTheme.RootPadding;
            _panel.style.paddingRight = DevToolsUiToolkitTheme.RootPadding;
            _panel.style.paddingBottom = DevToolsUiToolkitTheme.RootPadding;
            _panel.style.paddingLeft = DevToolsUiToolkitTheme.RootPadding;
            _root.Add(_panel);

            var header = _factory.CreateRow();
            header.style.alignItems = Align.Center;
            header.style.minHeight = DevToolsUiToolkitTheme.HeaderHeight;
            header.style.flexShrink = 0f;
            _panel.Add(header);

            _backButton = _factory.CreateActionButton(
                "Назад",
                DevToolsUiToolkitTheme.Surface,
                NavigateBack,
                compact: true);
            _backButton.style.width = 220f;
            header.Add(_backButton);

            _titleLabel = _factory.CreateTitle("Developer");
            _titleLabel.style.flexGrow = 1f;
            header.Add(_titleLabel);

            _closeButton = _factory.CreateActionButton(
                "X",
                DevToolsUiToolkitTheme.Surface,
                Close,
                compact: true);
            _closeButton.style.width = 120f;
            header.Add(_closeButton);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "devtools-scroll"
            };
            scroll.style.flexGrow = 1f;
            scroll.style.marginTop = 24f;
            scroll.contentContainer.style.alignItems = Align.Center;
            scroll.contentContainer.style.paddingBottom = 48f;
            _panel.Add(scroll);

            _pageViewport = scroll;
            _pageColumn = new VisualElement { name = "devtools-page-column" };
            _pageColumn.style.flexDirection = FlexDirection.Column;
            _pageColumn.style.width = DevToolsUiToolkitTheme.ContentWidthLandscape;
            _pageColumn.style.maxWidth = DevToolsUiToolkitTheme.ContentWidthLandscape;
            _pageColumn.style.minWidth = 320f;
            _pageColumn.style.flexShrink = 0f;
            scroll.Add(_pageColumn);

            RefreshHeader();
        }

        public VisualElement Root => _root;
        public DevToolsUiToolkitFactory Factory => _factory;
        public bool IsOpen => _panel.resolvedStyle.display != DisplayStyle.None;
        public string CurrentPageId => _currentPageId;

        public event Action Opened;
        public event Action Closed;

        public void AttachTo(VisualElement target)
        {
            if (target == null || _attachTarget == target)
                return;

            _root.RemoveFromHierarchy();
            _attachTarget = target;
            _attachTarget.Add(_root);
            RefreshLayout();
        }

        public void RegisterRootPage(string pageId, IDevToolsUiPage page)
        {
            _rootPageId = pageId;
            RegisterPage(pageId, page);
            Navigate(pageId);
        }

        public void RegisterPage(string pageId, IDevToolsUiPage page)
        {
            if (string.IsNullOrWhiteSpace(pageId))
                throw new ArgumentException("Page id is required.", nameof(pageId));
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            if (_pages.ContainsKey(pageId))
                throw new InvalidOperationException($"DEV page '{pageId}' is already registered.");

            page.Root.style.display = DisplayStyle.None;
            page.Root.style.flexDirection = FlexDirection.Column;
            _pages.Add(pageId, page);
            _pageColumn.Add(page.Root);
        }

        public void SetLauncherState(string title, bool danger)
        {
            _launcherButton.text = title;
            _launcherButton.style.backgroundColor = danger
                ? DevToolsUiToolkitTheme.LauncherDanger
                : DevToolsUiToolkitTheme.LauncherBackground;
        }

        public void Open()
        {
            if (_showLauncher)
            {
                _launcherButton.style.display = DisplayStyle.None;
                _panel.style.display = DisplayStyle.Flex;
            }

            if (string.IsNullOrWhiteSpace(_currentPageId) && !string.IsNullOrWhiteSpace(_rootPageId))
                Navigate(_rootPageId);
            Opened?.Invoke();
            Refresh();
        }

        public void Close()
        {
            if (_showLauncher)
            {
                _panel.style.display = DisplayStyle.None;
                _launcherButton.style.display = DisplayStyle.Flex;
            }

            Closed?.Invoke();
        }

        public void Navigate(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId) || !_pages.TryGetValue(pageId, out IDevToolsUiPage nextPage))
                return;

            if (!string.IsNullOrWhiteSpace(_currentPageId) &&
                _pages.TryGetValue(_currentPageId, out IDevToolsUiPage currentPage))
            {
                currentPage.OnHidden();
                currentPage.Root.style.display = DisplayStyle.None;
            }

            _currentPageId = pageId;
            nextPage.Root.style.display = DisplayStyle.Flex;
            nextPage.OnShown();
            RefreshHeader();
            Refresh();
        }

        public void Refresh()
        {
            if (!string.IsNullOrWhiteSpace(_currentPageId) && _pages.TryGetValue(_currentPageId, out IDevToolsUiPage currentPage))
                currentPage.Refresh();
            RefreshLayout();
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, IDevToolsUiPage> entry in _pages)
                entry.Value.Dispose();
            _pages.Clear();
            _root.RemoveFromHierarchy();
            Opened = null;
            Closed = null;
        }

        private void NavigateBack()
        {
            if (!string.IsNullOrWhiteSpace(_rootPageId) && _currentPageId != _rootPageId)
            {
                Navigate(_rootPageId);
                return;
            }

            Close();
        }

        private void RefreshHeader()
        {
            if (string.IsNullOrWhiteSpace(_currentPageId) || !_pages.TryGetValue(_currentPageId, out IDevToolsUiPage page))
            {
                _titleLabel.text = "Developer";
                _backButton.style.display = DisplayStyle.None;
                return;
            }

            _titleLabel.text = page.Title;
            _backButton.style.display = _currentPageId == _rootPageId
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void RefreshLayout()
        {
            Rect rect = _useSafeArea ? UiSafeArea.GetLocalRect(_root) : _root.contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float contentWidth = DevToolsUiToolkitTheme.ResolveContentWidth(rect);
            _pageColumn.style.width = contentWidth;
            _pageColumn.style.maxWidth = contentWidth;

            if (_showLauncher)
            {
                _launcherButton.style.left = rect.xMin + 18f;
                _launcherButton.style.top = rect.yMin + 18f;
            }
        }
    }
}
#endif