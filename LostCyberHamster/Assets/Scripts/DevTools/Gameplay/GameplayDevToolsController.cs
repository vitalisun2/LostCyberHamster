#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Обрабатывает ввод gameplay-экрана, выполняет действия сервиса и формирует состояние представления.
    /// </summary>
    internal sealed class GameplayDevToolsController
    {
        private readonly GameplayDevToolsService _service;
        private readonly GameplayDevToolsView _view;
        private readonly Action _openGameProgressTesting;
        private readonly Action _openExperienceProgressTesting;

        private bool _isBusy;
        private string _lastStatus = string.Empty;

        public GameplayDevToolsController(
            GameplayDevToolsService service,
            GameplayDevToolsView view,
            Action openGameProgressTesting,
            Action openExperienceProgressTesting)
        {
            _service = service;
            _view = view;
            _openGameProgressTesting = openGameProgressTesting;
            _openExperienceProgressTesting = openExperienceProgressTesting;

            _view.BotToggleRequested += ToggleBot;
            _view.UnlockAllToggleRequested += ToggleUnlockAll;
            _view.GameProgressTestingRequested += OpenGameProgressTesting;
            _view.ExperienceProgressTestingRequested +=
                OpenExperienceProgressTesting;
        }

        public void RefreshPresentation()
        {
            GameplayDevToolsSnapshot snapshot = _service.GetSnapshot();
            string status = GetStatus(snapshot);
            _view.Render(new GameplayDevToolsPresentation(
                snapshot.BotEnabled ? "Bot On" : "Bot Off",
                snapshot.UnlockAllLevels ? "Unlock All On" : "Unlock All Off",
                status,
                snapshot.BotEnabled,
                snapshot.UnlockAllLevels,
                snapshot.BotAvailable && !_isBusy,
                !_isBusy));
        }

        private void ToggleBot()
        {
            RunAction(_service.ToggleBot);
        }

        private void ToggleUnlockAll()
        {
            RunAction(_service.ToggleUnlockAll);
        }

        private void OpenGameProgressTesting()
        {
            if (!_isBusy)
                _openGameProgressTesting?.Invoke();
        }

        private void OpenExperienceProgressTesting()
        {
            if (!_isBusy)
                _openExperienceProgressTesting?.Invoke();
        }

        private void RunAction(Func<GameplayDevToolsActionResult> action)
        {
            if (_isBusy)
                return;

            _isBusy = true;
            RefreshPresentation();

            try
            {
                GameplayDevToolsActionResult result = action();
                _lastStatus = result.Message;
            }
            catch (Exception exception)
            {
                _lastStatus = $"Ошибка: {exception.Message}";
            }
            finally
            {
                _isBusy = false;
                RefreshPresentation();
            }
        }

        private string GetStatus(GameplayDevToolsSnapshot snapshot)
        {
            if (_isBusy)
                return "Выполняется...";

            if (!string.IsNullOrWhiteSpace(_lastStatus))
                return _lastStatus;

            return snapshot.BotAvailable ? string.Empty : "Bot is not ready";
        }
    }
}
#endif
