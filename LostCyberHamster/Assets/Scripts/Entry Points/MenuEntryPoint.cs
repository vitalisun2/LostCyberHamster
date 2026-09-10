using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.Tutorial;
using GameAds;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.Leaderboard;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.ReturnActivities;
using Zenject;

namespace Assets.Scripts.Entry_Points
{
    public class MenuEntryPoint : MonoBehaviour
    {
        [SerializeField]
        private UIDocument _uiDocument;

        private UIManager _uiManager;
        private AccountService _accountService;
        private ExistingAccountRestoreCoordinator _existingAccountRestoreCoordinator;
        private AccountPromptCoordinator _accountPromptCoordinator;
        private CloudSaveConflictCoordinator _cloudSaveConflictCoordinator;
        private CloudSyncService _cloudSyncService;
        private ConflictService _conflictService;
        private FirstSessionNotificationHost _notifications;
        private ShieldOnboardingController _shieldOnboarding;
        private NextGoalCardPresenter _nextGoal;
        private VisualElement _nextGoalRoot;
        private ScreenEnum _nextGoalScreen;
        private PlayerLevelPresentation _levelPresentation;
        private bool _levelCheckInProgress;
        private float _quietTime;
        private bool _applicationPaused;

        [Inject]
        private void Construct(
            AccountService accountService,
            ExistingAccountRestoreCoordinator existingAccountRestoreCoordinator,
            CloudSyncService cloudSyncService,
            ConflictService conflictService)
        {
            _accountService = accountService;
            _existingAccountRestoreCoordinator = existingAccountRestoreCoordinator;
            _cloudSyncService = cloudSyncService;
            _conflictService = conflictService;
        }

        private async Task Awake()
        {
            PlayerProgressLifecycleCheckpoint.EnsureCreated();

            // Потребляем одноразовую цель до создания контроллеров меню.
            var hasNavigationRequest = MenuNavigationRequest.TryConsume(
                out var requestedScreen,
                out var leaderboardLocationId,
                out var leaderboardPartId);
            var openLeaderboard =
                hasNavigationRequest &&
                requestedScreen == ScreenEnum.LeaderboardScreen &&
                !string.IsNullOrWhiteSpace(leaderboardLocationId) &&
                !string.IsNullOrWhiteSpace(leaderboardPartId);
            NextGoalNavigation.DiscardOtherDestination(hasNavigationRequest ? requestedScreen : ScreenEnum.HomeScreen);
            var selectLevelScreenController = new SelectLevelScreenController(_uiDocument);
            var leaderboardScreenController = new LeaderboardScreenController(_uiDocument, _cloudSyncService,
                (locationId, partId) =>
                {
                    selectLevelScreenController.SetInitialSelection(locationId, partId);
                    UIManager.OnScreenShow?.Invoke(ScreenEnum.SelectLevelScreen);
                },
                () => _uiManager != null && _uiManager.CurrentScreen == ScreenEnum.LeaderboardScreen &&
                    !_uiManager.HasModalOrTransition && !_uiManager.HasPriorityPresentation);
            if (openLeaderboard)
            {
                leaderboardScreenController.SetInitialSelection(
                    leaderboardLocationId,
                    leaderboardPartId);
            }

            // Создаём UI и сразу открываем запрошенный экран либо обычное главное меню.
            _uiManager = new UIManager(new IScreenController[]
            {
                new HomeScreenController(_uiDocument, OpenReturnActivities),
                new ReturnActivitiesScreenController(_uiDocument, ShowReturnActivityReward, () => SceneManager.LoadScene("Game")),
                new ActivityRewardModalController(_uiDocument, () => _uiManager.CloseModal(ScreenEnum.ActivityRewardModal)),
                new CharacterDevelopmentScreenController(_uiDocument, () => FirstSessionNavigation.Resume(_uiManager)),
                new SettingsScreenController(
                    _uiDocument,
                    _accountService,
                    _existingAccountRestoreCoordinator,
                    _cloudSyncService),
                new AccountPromptModalController(_uiDocument),
                new CloudSaveConflictModalController(_uiDocument),
                new CharacterScreenController(_uiDocument, () => FirstSessionNavigation.Resume(_uiManager)),
                new QuestsScreenController(_uiDocument),
                selectLevelScreenController,
                leaderboardScreenController,
                new ShopScreenController(_uiDocument),
                new LevelUpModalController(_uiDocument, () => _uiManager.CloseModal(ScreenEnum.LevelUpModal)),
                new DailyQuestRewardModalController(
                    _uiDocument,
                    CloseDailyQuestRewardModal),
            });

            await QuestManager.Init();
            ReturnActivityService.RefreshPeriods();
            _uiManager.GetController<QuestsScreenController>().SetDailyRewardGate(() =>
                _uiManager.CurrentScreen == ScreenEnum.QuestsScreen && !_uiManager.HasModalOrTransition &&
                !_uiManager.HasPriorityPresentation && !PlayerLevelPresentation.HasPendingLevel);
            await _uiManager.LoadScreenAsync(
                hasNavigationRequest
                    ? requestedScreen
                    : FirstSessionNavigation.HasReturnRoute && !GameDataManager.PlayerData.FirstSessionReturnFromLevelUp
                        ? ScreenEnum.CharacterDevelopmentScreen : ScreenEnum.HomeScreen);
            if (isActiveAndEnabled) CreateFirstSessionPresenters();
            _uiManager.HasPriorityPresentation = (PlayerLevelPresentation.HasPendingLevel || ShieldTutorialProgress.IsPending) &&
                !(_conflictService.CurrentConflict != null && !_cloudSyncService.IsConflictDeferred);
            _accountPromptCoordinator = new AccountPromptCoordinator(_uiManager, _accountService, _cloudSyncService);
            _cloudSaveConflictCoordinator = new CloudSaveConflictCoordinator(
                _uiManager,
                _cloudSyncService,
                _conflictService);
            if (isActiveAndEnabled)
            {
                _cloudSaveConflictCoordinator.Enable();
                _accountPromptCoordinator.Enable();
            }
            AdsManager.Initialize();
            LeaderboardReadService.Instance?.WarmMenuCache();
        }

        private void Start()
        {
            PlayerProgressCommitter.Commit(CheckpointReason.MenuEntered);

            // tutorial !GameDataManager.PlayerData.IsFirstLaunch

            GameDataManager.IsGameJustStarted = false;
        }

        private void OnEnable()
        {
            if (_cloudSaveConflictCoordinator != null) CreateFirstSessionPresenters();
            if (_uiManager != null) LeaderboardReadService.Instance?.WarmMenuCache();
            _uiManager?.SubscribeToEvents();
            _cloudSaveConflictCoordinator?.Enable();
            _accountPromptCoordinator?.Enable();
        }

        private void OnDisable()
        {
            _notifications?.Dispose();
            _shieldOnboarding?.Dispose();
            DisposeNextGoal();
            _levelPresentation?.Dispose();
            _notifications = null;
            _shieldOnboarding = null;
            _levelPresentation = null;
            LeaderboardReadService.Instance?.PauseMenuCache();
            _accountPromptCoordinator?.Disable();
            _cloudSaveConflictCoordinator?.Disable();
            _uiManager?.UnsubscribeFromEvents();
        }

        private void CloseDailyQuestRewardModal()
        {
            _uiManager.CloseModal(ScreenEnum.DailyQuestRewardModal);
        }

        private void OpenReturnActivities(string kind)
        {
            if (_uiManager.HasModalOrTransition || _uiManager.HasPriorityPresentation || PlayerLevelPresentation.HasPendingLevel) return;
            ReturnActivitiesScreenController.InitialKind = kind;
            UIManager.OnScreenShow?.Invoke(ScreenEnum.ReturnActivitiesScreen);
        }

        private async void ShowReturnActivityReward(ActivityRewardSnapshot reward)
        {
            if (reward?.IsCurrent != true || _uiManager.HasModalOrTransition || _uiManager.HasPriorityPresentation ||
                PlayerLevelPresentation.HasPendingLevel) return;
            try
            {
                _uiManager.GetController<ActivityRewardModalController>().SetReward(reward);
                await _uiManager.ShowModalAsync(ScreenEnum.ActivityRewardModal);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void CreateFirstSessionPresenters()
        {
            _levelPresentation ??= new PlayerLevelPresentation(_uiManager);
            _notifications ??= new FirstSessionNotificationHost(_uiManager, _uiDocument.rootVisualElement);
            _shieldOnboarding ??= new ShieldOnboardingController(_uiManager, _uiDocument.rootVisualElement);
        }

        private void Update()
        {
            if (_levelPresentation == null) return;
            if (!Application.isFocused || _applicationPaused)
            {
                _quietTime = 0;
                _shieldOnboarding.Tick(blocked: true);
                _nextGoal?.Tick(blocked: true);
                _notifications.Tick(gameplay: false, blocked: true);
                return;
            }
            bool conflict = _conflictService.CurrentConflict != null && !_cloudSyncService.IsConflictDeferred;
            if (!_levelCheckInProgress)
                _uiManager.HasPriorityPresentation = !conflict && PlayerLevelPresentation.HasPendingLevel;
            _cloudSaveConflictCoordinator?.Tick();
            bool blocked = conflict || _uiManager.HasModalOrTransition || TutorialStorage.IsPlayerDataBackupActive;
            _quietTime = blocked ? 0 : _quietTime + Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!blocked && _quietTime >= 0.75f && !_levelCheckInProgress && PlayerLevelPresentation.HasPendingLevel)
                PresentPendingLevel();
            _shieldOnboarding.Tick();
            _notifications.Tick(gameplay: false, blocked || _uiManager.HasPriorityPresentation || _shieldOnboarding.IsPresenting);
            _uiManager.HasPriorityPresentation |= _shieldOnboarding.IsPresenting;
            TickNextGoal(blocked || _uiManager.HasPriorityPresentation || _notifications.IsPresenting || _quietTime < .75f);
            _accountPromptCoordinator?.Tick();
        }

        private void TickNextGoal(bool blocked)
        {
            // Привязка принадлежит фактическому дереву экрана и освобождается при его замене.
            var screen = _uiManager.CurrentScreen;
            bool home = screen == ScreenEnum.HomeScreen;
            var host = home ? _uiDocument.rootVisualElement.Q("homescreen") :
                screen == ScreenEnum.SelectLevelScreen ? _uiDocument.rootVisualElement.Q("select-level-screen") : null;
            if (host != _nextGoalRoot || screen != _nextGoalScreen)
            {
                DisposeNextGoal();
                _nextGoalRoot = host;
                _nextGoalScreen = screen;
                if (host != null)
                    _nextGoal = new NextGoalCardPresenter(host,
                        home ? NextGoalCardPlacement.Home : NextGoalCardPlacement.SelectLevel,
                        goal => NextGoalNavigation.Open(_uiManager, goal));
            }
            _nextGoal?.Tick(blocked);
            if (home && host != null) host.EnableInClassList("home-screen--goal-space", _nextGoal?.HasGoal == true);
        }

        private void DisposeNextGoal()
        {
            _nextGoal?.Dispose();
            _nextGoal = null;
            _nextGoalRoot?.RemoveFromClassList("home-screen--goal-space");
            _nextGoalRoot = null;
        }

        private async void PresentPendingLevel()
        {
            _levelCheckInProgress = true;
            ScreenEnum returnScreen = _uiManager.CurrentScreen;
            try
            {
                bool resumeShield = GameDataManager.PlayerData.FirstSessionReturnToShield;
                await _levelPresentation.ShowAsync(() => { },
                    resumeShield ? null : () => { }, shield =>
                    {
                        if (shield || resumeShield)
                        {
                            if (!FirstSessionNavigation.HasReturnRoute)
                                FirstSessionNavigation.SetReturnRoute(null, returnScreen);
                            return FirstSessionNavigation.PrepareShield(_uiManager);
                        }
                        return FirstSessionNavigation.HasReturnRoute
                            ? FirstSessionNavigation.PrepareResume(_uiManager) : null;
                    });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _quietTime = 0;
                _levelCheckInProgress = false;
            }
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
            _quietTime = 0;
            _shieldOnboarding?.Tick(blocked: true);
            _nextGoal?.Tick(blocked: true);
            _notifications?.Tick(gameplay: false, blocked: true);
        }
    }
}
