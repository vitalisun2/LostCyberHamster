using System;
using System.Threading.Tasks;
using Extensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class WinModalController : ModalController
    {
        private VisualElement _resumeButton => _modalContent.Q<VisualElement>("btn__play");

        private VisualElement _restartButton => _modalContent.Q<VisualElement>("btn__repeat");

        private VisualElement _exitButton => _modalContent.Q<VisualElement>("btn__home");

        private VisualElement _starsContainer => _modalContent.Q<VisualElement>("stars_container");

        private Label _levelLocationLabel => _modalContent.Q<Label>("level_location");

        private Label _levelNameLabel => _modalContent.Q<Label>("level_name");

        private Action _actionResume;

        private Action _actionRestart;

        private Action _actionExit;

        protected override ScreenEnum _modalAssetName => ScreenEnum.WinModal;

        private string _locationName;
        private string _levelName;
        private int _stars;

        public WinModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override async Task OnShowAsync()
        {
            _buttonCloseModal.style.display = DisplayStyle.None;

            _levelNameLabel.text = _levelName;
            _levelLocationLabel.text = _locationName;
            var fullstar = AddressableExtentions.LoadAssetSync<Sprite>("star");
            for (int i = 1; i <= _stars; i++)
            {
                var star = _starsContainer.Q($"star{i}");
                star.style.backgroundImage = new StyleBackground(fullstar.texture);
            }
        }

        protected override void OnSubscribeToEvents()
        {
            _resumeButton?.RegisterCallback<ClickEvent>(OnClickResume);
            _restartButton?.RegisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.RegisterCallback<ClickEvent>(OnClickExit);

        }

        private void OnClickExit(ClickEvent evt)
        {
            _actionExit?.Invoke();
        }


        private void OnClickRestart(ClickEvent evt)
        {
            _actionRestart?.Invoke();
        }


        private void OnClickResume(ClickEvent evt)
        {
            _actionResume?.Invoke();
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _resumeButton?.UnregisterCallback<ClickEvent>(OnClickResume);
            _restartButton?.UnregisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.UnregisterCallback<ClickEvent>(OnClickExit);
        }

        public void SetResumeAction(Action value)
        {
            _actionResume = value;
        }

        public void SetRestartAction(Action value)
        {
            _actionRestart = value;
        }

        public void SetExitAction(Action value)
        {
            _actionExit = value;
        }

        public void SetParamsForInit(string locationName, string levelName, int stars)
        {
            _locationName = locationName;
            _levelName = levelName;
            _stars = stars;
        }
    }
}