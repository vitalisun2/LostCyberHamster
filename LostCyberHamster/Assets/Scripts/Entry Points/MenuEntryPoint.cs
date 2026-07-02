using System;
using System.Threading.Tasks;
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

        private async Task Awake()
        {
            _uiManager = new UIManager(new IScreenController[]
            {
                new HomeScreenController(_uiDocument),
                new SettingsModalController(_uiDocument),
                new CharacterScreenController(_uiDocument),
                new QuestsScreenController(_uiDocument),
                new SelectLevelScreenController(_uiDocument),
                new ShopModalController(_uiDocument),
                new DailyTasksScreenController(_uiDocument),
                new SigninModalController(_uiDocument),
            });

            await _uiManager.LoadScreenAsync(ScreenEnum.HomeScreen);
            AdsManager.Initialize();
            await QuestManager.Init();
        }

        private async void Start()
        {
            GameDataManager.PlayerData.Crystals = CrystalStorage.GetCurrentBalance();
            GameDataManager.PlayerData.Money = MoneyStorage.GetCurrentBalance();

            GameDataManager.SaveData();

            // tutorial !GameDataManager.PlayerData.IsFirstLaunch

            GameDataManager.IsGameJustStarted = false;
        }

        private void OnEnable()
        {
            _uiManager?.SubscribeToEvents();
        }

        private void OnDisable()
        {
            _uiManager?.UnsubscribeFromEvents();
        }
    }
}
