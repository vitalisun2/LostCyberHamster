using System;
using System.Threading.Tasks;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>Доставляет накопленный Level Up перед выбранным переходом результата.</summary>
    internal sealed class LevelResultNavigationCoordinator : IDisposable
    {
        private readonly UIManager _uiManager;
        private readonly PlayerLevelPresentation _presentation;
        private bool _continuing;
        private bool _disposed;

        public void Dispose()
        {
            _disposed = true;
            _presentation.Dispose();
        }

        public LevelResultNavigationCoordinator(UIManager uiManager)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _presentation = new PlayerLevelPresentation(uiManager);
        }

        public async void Continue(ScreenEnum sourceModal, Action action, string returnLevel = null,
            ScreenEnum returnScreen = ScreenEnum.HomeScreen, string location = null, string part = null,
            bool startShieldLesson = false)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_continuing || _disposed) return;
            _continuing = true;
            int scene = SceneManager.GetActiveScene().handle;
            var profile = GameDataManager.ProfileId;
            long generation = GameDataManager.Generation;
            bool actionInvoked = false;
            bool IsCurrent() => !_disposed && SceneManager.GetActiveScene().handle == scene &&
                profile == GameDataManager.ProfileId && generation == GameDataManager.Generation;

            async void InvokeOnce(Action continuation)
            {
                if (_disposed || actionInvoked || SceneManager.GetActiveScene().handle != scene ||
                    profile != GameDataManager.ProfileId || generation != GameDataManager.Generation) return;
                actionInvoked = true;
                try
                {
                    await GameAds.InterstitialAdService.Instance.ShowSelectedAfterResultsAsync(IsCurrent);
                }
                catch (Exception exception) { Debug.LogException(exception); }
                try { if (IsCurrent()) continuation(); }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    await RestoreResult();
                }
            }

            async Task RestoreResult()
            {
                if (!IsCurrent()) return;
                _continuing = false;
                try
                {
                    await _uiManager.ShowModalAsync(sourceModal);
                    if (IsCurrent() && _uiManager.CurrentModal != sourceModal && !_uiManager.HasModalOrTransition)
                        SceneManager.LoadScene("Menu");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    // Сохранённое направление и pending Level Up восстановятся в меню.
                    if (IsCurrent()) SceneManager.LoadScene("Menu");
                }
            }

            try
            {
                if (PlayerLevelPresentation.HasPendingLevel)
                    GameDataManager.ExecuteTransaction(CheckpointReason.FirstSessionTutorialProgressed,
                        () => FirstSessionNavigation.SetReturnRoute(returnLevel, returnScreen, location, part,
                            awaitingLevelUp: true, startShieldLesson));
                _uiManager.CloseModal(sourceModal);
                bool shown = await _presentation.ShowAsync(() => InvokeOnce(action),
                    startShieldLesson ? null : () => { }, shield => shield || startShieldLesson
                        ? FirstSessionNavigation.PrepareShield(_uiManager)
                        : FirstSessionNavigation.PrepareResume(_uiManager));
                if (!shown)
                {
                    if (PlayerLevelPresentation.HasPendingLevel) await RestoreResult();
                    else InvokeOnce(action);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                await RestoreResult();
            }
        }
    }
}
