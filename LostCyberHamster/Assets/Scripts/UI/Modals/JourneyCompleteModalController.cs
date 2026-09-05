using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает итог путешествия и маршруты в основные meta-экраны.
    /// </summary>
    public sealed class JourneyCompleteModalController : ModalController
    {
        private const float DesignWidth = 1724f;
        private const float DesignHeight = 912f;

        private Action _homeAction;
        private Action _skillsAction;
        private Action _rankingsAction;
        private GameResultModalPresentation _presentation;
        private IVisualElementScheduledItem _layoutTask;

        private VisualElement Viewport =>
            _modalContent.Q<VisualElement>("journey-complete-viewport");
        private VisualElement ScaleFrame =>
            _modalContent.Q<VisualElement>("journey-complete-scale-frame");
        private VisualElement Design =>
            _modalContent.Q<VisualElement>("journey-complete-design");
        private Button HomeButton =>
            _modalContent.Q<Button>("journey-complete-home");
        private Button SkillsButton =>
            _modalContent.Q<Button>("journey-complete-skills");
        private Button RankingsButton =>
            _modalContent.Q<Button>("journey-complete-rankings");

        protected override ScreenEnum _modalAssetName =>
            ScreenEnum.JourneyCompleteModal;

        public JourneyCompleteModalController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        public void SetHomeAction(Action action)
        {
            _homeAction = action;
        }

        public void SetSkillsAction(Action action)
        {
            _skillsAction = action;
        }

        public void SetRankingsAction(Action action)
        {
            _rankingsAction = action;
        }

        protected override Task OnShowAsync()
        {
            _presentation?.Restore();
            _presentation = GameResultModalPresentation.Apply(_root);
            _buttonCloseModal.style.display = DisplayStyle.None;
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _presentation ??= GameResultModalPresentation.Apply(_root);
            HomeButton?.RegisterCallback<ClickEvent>(OnHomeClicked);
            SkillsButton?.RegisterCallback<ClickEvent>(OnSkillsClicked);
            RankingsButton?.RegisterCallback<ClickEvent>(OnRankingsClicked);
            Viewport?.RegisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
            _layoutTask?.Pause();
            _layoutTask = Viewport?.schedule.Execute(() =>
            {
                if (_presentation != null && Viewport != null)
                    ApplyResponsiveLayout(Viewport.contentRect.size);
            });
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _layoutTask?.Pause();
            _layoutTask = null;
            HomeButton?.UnregisterCallback<ClickEvent>(OnHomeClicked);
            SkillsButton?.UnregisterCallback<ClickEvent>(OnSkillsClicked);
            RankingsButton?.UnregisterCallback<ClickEvent>(OnRankingsClicked);
            Viewport?.UnregisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
            _presentation?.Restore();
            _presentation = null;
        }

        private void OnHomeClicked(ClickEvent _)
        {
            _homeAction?.Invoke();
        }

        private void OnSkillsClicked(ClickEvent _)
        {
            _skillsAction?.Invoke();
        }

        private void OnRankingsClicked(ClickEvent _)
        {
            _rankingsAction?.Invoke();
        }

        private void OnViewportGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveLayout(evt.newRect.size);
        }

        private void ApplyResponsiveLayout(Vector2 viewportSize)
        {
            float width = Mathf.Max(1f, viewportSize.x);
            float height = Mathf.Max(1f, viewportSize.y);
            float scale = Mathf.Min(
                width / DesignWidth,
                height / DesignHeight);

            ScaleFrame.style.width = DesignWidth * scale;
            ScaleFrame.style.height = DesignHeight * scale;
            Design.style.scale = new Scale(
                new Vector3(scale, scale, 1f));
        }
    }
}
