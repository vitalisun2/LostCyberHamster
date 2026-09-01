using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public class PauseModalController : ModalController
    {
        private VisualElement _resumeButton => _modalContent.Q<VisualElement>("btn__play");

        private VisualElement _restartButton => _modalContent.Q<VisualElement>("btn__repeat");

        private VisualElement _exitButton => _modalContent.Q<VisualElement>("btn__home");

        private Action _actionResume;

        private Action _actionRestart;

        private Action _actionExit;

        private GameResultModalPresentation _presentation;

        protected override ScreenEnum _modalAssetName => ScreenEnum.PauseModal;

        public PauseModalController(UIDocument uiDocument): base(uiDocument)
        {
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
            _presentation?.Restore();
            _presentation = null;
        }

        internal void SetResumeAction(Action value)
        {
            _actionResume = value;
        }

        internal void SetRestartAction(Action value)
        {
            _actionRestart = value;
        }

        internal void SetExitAction(Action value)
        {
            _actionExit = value;
        }
    }
}
