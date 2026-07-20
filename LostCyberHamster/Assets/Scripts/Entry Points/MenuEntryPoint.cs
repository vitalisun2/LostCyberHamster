using System;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameAds;
using GameManagement;
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
        private AccountPromptCoordinator _accountPromptCoordinator;

        [Inject]
        private void Construct(AccountService accountService)
        {
            _accountService = accountService;
        }

        private async Task Awake()
        {
            PlayerProgressLifecycleCheckpoint.EnsureCreated();

            _uiManager = new UIManager(new IScreenController[]
            {
                new HomeScreenController(_uiDocument),
                new SettingsModalController(_uiDocument, _accountService),
                new AccountPromptModalController(_uiDocument),
                new CharacterScreenController(_uiDocument),
                new QuestsScreenController(_uiDocument),
                new SelectLevelScreenController(_uiDocument),
                new ShopModalController(_uiDocument),
                new DailyTasksScreenController(_uiDocument),
            });

            await _uiManager.LoadScreenAsync(ScreenEnum.HomeScreen);
            _accountPromptCoordinator = new AccountPromptCoordinator(_uiManager, _accountService);
            if (isActiveAndEnabled)
                _accountPromptCoordinator.Enable();
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
            _accountPromptCoordinator?.Enable();
        }

        private void OnDisable()
        {
            _accountPromptCoordinator?.Disable();
            _uiManager?.UnsubscribeFromEvents();
        }
    }
}
