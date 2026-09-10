using System.Threading.Tasks;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using Assets.Scripts.GameEngine.Mechanics;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace Assets.Scripts.Gameplay
{
    public class GameUi : MonoBehaviour
    {
        private bool _isInitialized;
        private bool _destroyed;
        private bool _applicationPaused;
        private bool _runtimeSubscribed;

        private Hamster _character;
        private GameManager _gameManager;
        private UIDocument _uiDocument;

        private UIManager _uiManager { get; set; }

        // mechanics
        private UiGameScreenMechanics _uiGameScreenMechanics;
        private UiPauseScreenMechanics _uiPauseScreenMechanics;
        private EnergyMechanics _energyMechanics;
        private UiGameOverMechanics _uiGameOverMechanics;
        private UiLoseModalMechanics _uiLoseModalMechanics;
        private UiWinModalMechanics _uiWinModalMechanics;
        private UiJourneyCompleteModalMechanics
            _uiJourneyCompleteModalMechanics;
        private LevelResultNavigationCoordinator
            _levelResultNavigationCoordinator;
        private KeyboardMechanics _keyboardMechanics;
        private FirstSessionNotificationHost _notifications;
        private ShieldPracticeController _shieldPractice;

        [Inject]
        public async Task Construct()
        {
            _character = LevelController.Instance.LevelData.Hamster;
            _gameManager = LevelController.Instance.LevelData.GameManager;
            _uiDocument = GetComponent<UIDocument>();


            await Initialize();
        }

        private async Task Initialize()
        {
            _uiManager = new UIManager(new IScreenController[]
            {
                new GameScreenController(_uiDocument),
                new PauseModalController(_uiDocument),
                new LoseModalController(_uiDocument),
                new WinModalController(_uiDocument),
                new JourneyCompleteModalController(_uiDocument),
                new LevelUpModalController(
                    _uiDocument,
                    CloseLevelUpModal),
            });

            _uiGameScreenMechanics = new UiGameScreenMechanics(_uiManager, _gameManager, _character);
            _uiPauseScreenMechanics = new UiPauseScreenMechanics(_uiManager, _gameManager);
            _energyMechanics = new EnergyMechanics(
                _character.Energy,
                _character.JumpRequest,
                _character.RoofJumpRequest,
                _character.SuperJumpRequest,
                _character.SuperRoofJumpRequest,
                _character.ActorSwitcher);
            _uiGameOverMechanics = new UiGameOverMechanics(_uiManager, _gameManager, _character,
                () => _uiGameScreenMechanics.GrossCollectedCoins);
            _levelResultNavigationCoordinator =
                new LevelResultNavigationCoordinator(_uiManager);
            _uiLoseModalMechanics = new UiLoseModalMechanics(_uiManager, _gameManager, _character,
                _levelResultNavigationCoordinator);
            _uiWinModalMechanics = new UiWinModalMechanics(
                _uiManager,
                _levelResultNavigationCoordinator);
            _uiJourneyCompleteModalMechanics =
                new UiJourneyCompleteModalMechanics(
                    _uiManager,
                    _levelResultNavigationCoordinator);
            _keyboardMechanics = new KeyboardMechanics(_character, _uiManager);

            await _uiManager.LoadScreenAsync(ScreenEnum.GameScreen);
            if (_destroyed) return;
            _isInitialized = true;
            if (isActiveAndEnabled) ActivateRuntime();
        }

        private void CloseLevelUpModal()
        {
            _uiManager.CloseModal(ScreenEnum.LevelUpModal);
        }

        private void Update()
        {
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.GameUiUpdate);
            if(!_isInitialized)
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.GameUiUpdate,
                    allocationSample);
                return;
            }

            _uiGameScreenMechanics.OnUpdate();
            _keyboardMechanics.OnUpdate();
            _energyMechanics.OnUpdate(Time.deltaTime);
            bool blocked = _gameManager.State != GameState.PLAYING || _uiManager.HasModalOrTransition ||
                TutorialStorage.IsPlayerDataBackupActive || !Application.isFocused || _applicationPaused;
            if (!blocked && !_shieldPractice.IsPresenting)
                GameAds.InterstitialAdService.Instance.RecordActiveGameplay(Time.unscaledDeltaTime);
            _shieldPractice.Tick(blocked);
            _notifications.Tick(gameplay: true, blocked || _shieldPractice.IsPresenting);
            RuntimePerformanceDiagnostics.EndAllocationSample(
                RuntimePerformanceScope.GameUiUpdate,
                allocationSample);
        }

        private void OnDisable()
        {
            _notifications?.Dispose();
            _shieldPractice?.Dispose();
            _notifications = null;
            _shieldPractice = null;
            _runtimeSubscribed = false;
            _uiManager?.UnsubscribeFromEvents();
            _energyMechanics?.Unsubscribe();
            _uiGameScreenMechanics?.Unsubscribe();
            _uiGameOverMechanics?.Unsubscribe();
        }

        private void OnEnable()
        {
            if (!_isInitialized) return;
            ActivateRuntime();
        }

        private void ActivateRuntime()
        {
            if (_runtimeSubscribed) return;
            _runtimeSubscribed = true;
            _uiManager.SubscribeToEvents();
            _energyMechanics.Subscribe();
            _uiGameScreenMechanics.Subscribe();
            _uiGameOverMechanics.Subscribe();
            _uiGameScreenMechanics.SyncState();
            _notifications ??= new FirstSessionNotificationHost(_uiManager, _uiDocument.rootVisualElement, _character);
            _shieldPractice ??= new ShieldPracticeController(_character, _uiDocument.rootVisualElement);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _levelResultNavigationCoordinator?.Dispose();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused) HideFirstSessionOverlays();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) HideFirstSessionOverlays();
        }

        private void HideFirstSessionOverlays()
        {
            _shieldPractice?.Tick(blocked: true);
            _notifications?.Tick(gameplay: true, blocked: true);
        }
    }
}
