using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameAds;
using GameManagement;
using Assets.Scripts.Tutorial;
using LostCyberHamster.UI;
using UnityEngine.SceneManagement;
using Vues.GameCore;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>Воскрешает только исходную живую попытку после сохранённого результата рекламы.</summary>
    public class UiLoseModalMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _character;
        private readonly LoseModalController _loseModalController;
        private readonly string _runId = Guid.NewGuid().ToString("N");
        private readonly int _sceneHandle;
        private RewardedAdRequest _request;
        private bool _runEnded;
        private bool _purchasing;
        private readonly string _profile = GameDataManager.ProfileId;
        private readonly long _generation = GameDataManager.Generation;
        private readonly LevelResultNavigationCoordinator _navigation;

        internal UiLoseModalMechanics(UIManager uiManager, GameManager gameManager, Hamster character,
            LevelResultNavigationCoordinator navigation)
        {
            _uiManager = uiManager;
            _navigation = navigation;
            _gameManager = gameManager;
            _character = character;
            _sceneHandle = character.gameObject.scene.handle;
            _loseModalController = _uiManager.GetController<LoseModalController>();
            _loseModalController.SetExitAction(OnExit);
            _loseModalController.SetRestartAction(OnRestart);
            _loseModalController.SetWatchAdsAction(OnWatchAd);
            _loseModalController.SetReviveActions(CanContinue, CanBuyRevive, OnBuyRevive);
        }

        private void OnExit()
        {
            EndAttempt();
            _navigation.Continue(ScreenEnum.LoseModal, () => SceneManager.LoadScene("Menu"));
        }

        private void OnRestart()
        {
            EndAttempt();
            _navigation.Continue(ScreenEnum.LoseModal, () => LevelController.Instance.Replay(),
                GameDataManager.PlayerData.CurrentLevel);
        }

        private void EndAttempt()
        {
            if (_runEnded) return;
            FirstSessionTelemetry.Record("attempt_failed", Vues.GameCore.QuestManager.CurrentAttemptPreview.AttemptId);
            _runEnded = true;
            RewardedAdService.Instance.CancelContext(_request);
            _request = null;
        }

        private void OnWatchAd()
        {
            if (!CanContinue() || _purchasing || (_request != null && !_request.IsFinished))
                return;
            _request = RewardedAdService.Instance.RequestRevive(_runId, _sceneHandle,
                CanContinue,
                Revive);
            _loseModalController.SetAdvertisementRequest(_request);
        }

        private bool CanContinue() => !_runEnded && _profile == GameDataManager.ProfileId &&
            _generation == GameDataManager.Generation && _character != null && _gameManager != null &&
            _character.gameObject.scene.handle == _sceneHandle && _character.Lives.Value <= 0 &&
            GameDataManager.PlayerData?.Monetization?.LastRevivedRunId != _runId;

        private bool CanBuyRevive() => CanContinue() && !_purchasing && !RewardedAdService.Instance.IsBusy &&
            !GameDataManager.IsProfileReplacementBlocked && !TutorialStorage.IsPlayerDataBackupActive &&
            !Assets.Scripts.Account.AccountTransitionScope.IsActive &&
            ResourceManager.CanSpendResource(ResourceType.Crystals, 1);

        private void OnBuyRevive()
        {
            if (!CanBuyRevive()) return;
            _purchasing = true;
            bool committed = false;
            try
            {
                GameDataManager.ExecuteTransaction(CheckpointReason.RunRevivePurchased, () =>
                {
                    if (!CanContinue() || !ResourceManager.SpendResource(ResourceType.Crystals, 1, notify: false))
                        throw new InvalidOperationException("Revive context or balance changed.");
                    GameDataManager.PlayerData.Monetization ??= new MonetizationState();
                    GameDataManager.PlayerData.Monetization.LastRevivedRunId = _runId;
                }, () => committed = true);
                if (!committed) return;
                MonetizationEvent.Record("crystal_spent", "revive", _runId, 1);
                ResourceManager.NotifyBalancesChangedAfterCommit();
                Revive();
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { _purchasing = false; }
        }

        private void Revive()
        {
            FirstSessionTelemetry.Record("attempt_revived", Vues.GameCore.QuestManager.CurrentAttemptPreview.AttemptId);
            _character.Lives.Value = 1;
            _uiManager.CloseModal(ScreenEnum.LoseModal);
            _gameManager.Resume();
        }
    }
}
