using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
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
            var openLeaderboard = MenuNavigationRequest.TryConsumeLeaderboard(
                out var leaderboardLocationId,
                out var leaderboardPartId);
            var leaderboardScreenController = new LeaderboardScreenController(
                _uiDocument,
                new LeaderboardService());
            if (openLeaderboard)
            {
                leaderboardScreenController.SetInitialSelection(
                    leaderboardLocationId,
                    leaderboardPartId);
            }

            // Создаём UI и сразу открываем запрошенный экран либо обычное главное меню.
            _uiManager = new UIManager(new IScreenController[]
            {
                new HomeScreenController(_uiDocument),
                new SuperAttacksScreenController(_uiDocument),
                new SettingsScreenController(
                    _uiDocument,
                    _accountService,
                    _existingAccountRestoreCoordinator,
                    _cloudSyncService),
                new AccountPromptModalController(_uiDocument),
                new CloudSaveConflictModalController(_uiDocument),
                new CharacterScreenController(_uiDocument),
                new QuestsScreenController(_uiDocument),
                new SelectLevelScreenController(_uiDocument),
                leaderboardScreenController,
                new ShopModalController(_uiDocument),
                new DailyTasksScreenController(_uiDocument),
            });

            await _uiManager.LoadScreenAsync(
                openLeaderboard
                    ? ScreenEnum.LeaderboardScreen
                    : ScreenEnum.HomeScreen);
            _accountPromptCoordinator = new AccountPromptCoordinator(_uiManager, _accountService);
            _cloudSaveConflictCoordinator = new CloudSaveConflictCoordinator(
                _uiManager,
                _conflictService);
            if (isActiveAndEnabled)
            {
                _cloudSaveConflictCoordinator.Enable();
                _accountPromptCoordinator.Enable();
            }
            AdsManager.Initialize();
            await QuestManager.Init();
        }

        private void Start()
        {
            PlayerProgressCommitter.Commit(CheckpointReason.MenuEntered);

            // tutorial !GameDataManager.PlayerData.IsFirstLaunch

            GameDataManager.IsGameJustStarted = false;
        }

        private void OnEnable()
        {
            _uiManager?.SubscribeToEvents();
            _cloudSaveConflictCoordinator?.Enable();
            _accountPromptCoordinator?.Enable();
        }

        private void OnDisable()
        {
            _accountPromptCoordinator?.Disable();
            _cloudSaveConflictCoordinator?.Disable();
            _uiManager?.UnsubscribeFromEvents();
        }
    }
}
