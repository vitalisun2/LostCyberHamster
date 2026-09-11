using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>Общий маршрут паузы HUD и клавиатуры, включая текущий шаг обучения.</summary>
    public class UiPauseScreenMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly PauseModalController _pauseModalController;
        private TutorialFlowController _tutorial;
        private bool _opening;
        private bool _leaving;

        public UiPauseScreenMechanics(UIManager uiManager, GameManager gameManager)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;

            _pauseModalController = _uiManager.GetController<PauseModalController>();

            // Обе поверхности ввода используют одну защиту повторного открытия.
            uiManager.GetController<GameScreenController>().SetPauseAction(TogglePause);
            _pauseModalController.SetResumeAction(OnResume);
            _pauseModalController.SetExitAction(OnExit);
            _pauseModalController.SetRestartAction(OnRestart);
        }

        private async void TogglePause()
        {
            if (_opening || _leaving || UiInputBlock.IsBlocked || _gameManager == null)
                return;
            if (_uiManager.CurrentModal == ScreenEnum.PauseModal)
            {
                OnResume();
                return;
            }
            if (_uiManager.CurrentModal.HasValue)
                return;

            // Tutorial сохраняет остановку подсказки; обычная игра должна быть запущена.
            var tutorial = TutorialRuntimeHost.ActiveFlow;
            if (tutorial != null)
            {
                if (!tutorial.PauseByUser())
                    return;
            }
            else
            {
                if (GameplayInputGate.IsBlocked || _gameManager.State != GameState.PLAYING)
                    return;
                _gameManager.Pause();
            }
            _tutorial = tutorial;
            _opening = true;
            _pauseModalController.SetTutorialMode(tutorial != null);

            // Ошибка или отмена открытия возвращает прежнее состояние мира и урока.
            try
            {
                await _uiManager.ShowModalAsync(ScreenEnum.PauseModal);
                if (_gameManager != null && _uiManager.CurrentModal != ScreenEnum.PauseModal)
                    RestoreGameplay();
            }
            catch (Exception exception)
            {
                if (_gameManager != null)
                {
                    _pauseModalController.UnsubscribeFromEvents();
                    _pauseModalController.Close();
                    RestoreGameplay();
                    Debug.LogWarning($"Cannot open pause: {exception.GetType().Name}.");
                }
            }
            finally
            {
                _opening = false;
            }
        }

        private void OnResume()
        {
            if (_opening || _leaving || UiInputBlock.IsBlocked || _gameManager == null ||
                (_tutorial != null && !_tutorial.CanResumeByUser))
                return;
            _uiManager.CloseModal(ScreenEnum.PauseModal);
            RestoreGameplay();
        }

        private void RestoreGameplay()
        {
            if (_tutorial != null)
                _tutorial.ResumeByUser();
            else
                _gameManager.Resume();
            _tutorial = null;
        }

        private void OnExit()
        {
            if (_leaving || UiInputBlock.IsBlocked)
                return;
            _leaving = true;

            // Выход из обучения сохраняет rollback до загрузки меню.
            try
            {
                if (_tutorial != null)
                    _tutorial.ExitToMenu();
                else
                {
                    Assets.Scripts.Diagnostics.EconomyTelemetry.FinishRun("exit");
                    SceneManager.LoadScene("Menu");
                }
            }
            catch (Exception exception)
            {
                _leaving = false;
                _pauseModalController.ShowExitError(_tutorial == null || _tutorial.CanResumeByUser);
                Debug.LogWarning($"Cannot leave paused game: {exception.GetType().Name}.");
            }
        }

        private void OnRestart()
        {
            if (_leaving || _tutorial != null || UiInputBlock.IsBlocked)
                return;
            _leaving = true;
            Assets.Scripts.Diagnostics.EconomyTelemetry.FinishRun("restart");
            _uiManager.CloseModal(ScreenEnum.PauseModal);
            LevelController.Instance.Replay();
        }
    }
}
