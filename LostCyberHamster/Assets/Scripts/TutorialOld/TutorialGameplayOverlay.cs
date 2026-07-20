using System;
using UnityEngine.UIElements;

namespace Assets.Scripts.TutorialOld
{
    public sealed class TutorialGameplayOverlay : IDisposable
    {
        private TutorialUiController _uiController;
        private Action _skipAction;
        private Action _completePlayAction;
        private Action _completeMenuAction;
        private Action<TutorialAction> _gameplayAction;

        public void Attach(VisualElement root)
        {
            if (root == null || _uiController != null)
            {
                return;
            }

            _uiController = new TutorialUiController(
                root,
                root.Q<VisualElement>("tap"),
                root.Q<VisualElement>("btn_jump"),
                root.Q<VisualElement>("btn_ultra"));
            SyncActions();
            _uiController.SubscribeToEvents();
            _uiController.Hide();
        }

        public void SetActions(Action skipAction, Action playAction, Action menuAction)
        {
            _skipAction = skipAction;
            _completePlayAction = playAction;
            _completeMenuAction = menuAction;
            SyncActions();
        }

        public void SetGameplayAction(Action<TutorialAction> gameplayAction)
        {
            _gameplayAction = gameplayAction;
            SyncActions();
        }

        public void ShowHeader(string title)
        {
            _uiController?.ShowHeader(title);
        }

        public void ShowPrompt(string instruction, TutorialAction focusAction)
        {
            _uiController?.ShowPrompt(instruction, focusAction);
        }

        public void HidePrompt()
        {
            _uiController?.HidePrompt();
        }

        public void ShowComplete(string title, string message)
        {
            _uiController?.ShowComplete(title, message);
        }

        public void ShowComplete(
            string title,
            string message,
            string playButtonText,
            string menuButtonText,
            bool showPlayButton)
        {
            _uiController?.ShowComplete(
                title,
                message,
                playButtonText,
                menuButtonText,
                showPlayButton);
        }

        public void Hide()
        {
            _uiController?.Hide();
        }

        public void Dispose()
        {
            _uiController?.UnsubscribeFromEvents();
            _uiController = null;
        }

        private void SyncActions()
        {
            _uiController?.SetActions(
                _skipAction,
                _completePlayAction,
                _completeMenuAction);
            _uiController?.SetGameplayAction(_gameplayAction);
        }
    }
}
