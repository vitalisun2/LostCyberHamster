using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Atomic.Elements;
using GameManagement;
using GameManagement.Progress;
using LostCyberHamster.UI;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiGameOverMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _hamster;
        private readonly RunExperienceCollector _runExperienceCollector;
        private readonly XpRewardBannerController
            _xpRewardBannerController;
        private bool _isLevelCompletionHandled;
        private bool _isRunResultResolved;
        private bool _isRunExperienceCollected;
        private bool _isWinModalShown;
        private bool _isExperienceBannerStarted;
        private RunExperienceResult _runExperienceResult;

        public UiGameOverMechanics(
            UIManager uiManager,
            GameManager gameManager,
            Hamster hamster,
            XpRewardBannerController xpRewardBannerController)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _hamster = hamster;
            _xpRewardBannerController =
                xpRewardBannerController;
            _runExperienceCollector = new RunExperienceCollector(
                GameDataManager.PlayerData);
        }

        public void Subscribe()
        {
            _gameManager.OnFinish += OnFinish;
            _hamster.RunResultChanged += OnRunResultChanged;
        }

        public void Unsubscribe()
        {
            _gameManager.OnFinish -= OnFinish;
            _hamster.RunResultChanged -= OnRunResultChanged;
        }

        private async void OnFinish()
        {
            if(_hamster.Lives.Value == 0)
            {
                await _uiManager.ShowModalAsync(
                    ScreenEnum.LoseModal);
            }
            else
            {
                // Сохраняем одноразовое состояние первого завершения уровня.
                var completedLevelNumber = LevelManager.GetCurrentLevelNumber();
                var playerData = GameDataManager.PlayerData;
                if (completedLevelNumber == 1 &&
                    playerData != null &&
                    !playerData.IsAccountPromptPending &&
                    !playerData.IsAccountPromptShown)
                {
                    playerData.IsAccountPromptPending = true;
                }

                // Выдаём синхронную XP-награду за улучшенные звёзды.
                GameEventsManager.LevelCompleted(completedLevelNumber, _hamster.Lives.Value);
                _isLevelCompletionHandled = true;
                TryCollectRunExperience();

                // Показываем WinModal до запуска отдельного XP overlay.
                var winScreenController = _uiManager.GetController<WinModalController>();
                winScreenController.SetParamsForInit(LevelManager.GetLocationName(), LevelManager.GetCurrentPartOfDay(), _hamster.Lives.Value);
                RunResultData latestRunResult =
                    _hamster.LatestRunResult;
                winScreenController.SetRunResult(latestRunResult);
                if (latestRunResult == null ||
                    latestRunResult.SubmissionState !=
                    RunResultSubmissionState.Pending)
                {
                    _isRunResultResolved = true;
                    TryCollectRunExperience();
                }

                await _uiManager.ShowModalAsync(
                    ScreenEnum.WinModal);
                _isWinModalShown = true;
                TryStartExperienceBanner();
            }
        }

        private void OnRunResultChanged(RunResultData result)
        {
            _uiManager
                .GetController<WinModalController>()
                .SetRunResult(result);

            if (result != null &&
                result.SubmissionState !=
                RunResultSubmissionState.Pending)
            {
                _isRunResultResolved = true;
                TryCollectRunExperience();
            }
        }

        private void TryCollectRunExperience()
        {
            if (_isRunExperienceCollected ||
                !_isLevelCompletionHandled ||
                !_isRunResultResolved)
            {
                return;
            }

            _runExperienceResult =
                _runExperienceCollector.Collect(
                    GameDataManager.PlayerData);
            _isRunExperienceCollected = true;
            TryStartExperienceBanner();
        }

        private void TryStartExperienceBanner()
        {
            if (_isExperienceBannerStarted ||
                !_isWinModalShown ||
                _runExperienceResult == null)
            {
                return;
            }

            _isExperienceBannerStarted = true;
            _ = _xpRewardBannerController.ShowAsync(
                _runExperienceResult);
        }
    }
}
