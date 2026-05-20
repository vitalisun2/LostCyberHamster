using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
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
        private KeyboardMechanics _keyboardMechanics;

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
            });

            _uiGameScreenMechanics = new UiGameScreenMechanics(_uiManager, _gameManager, _character);
            _uiPauseScreenMechanics = new UiPauseScreenMechanics(_uiManager, _gameManager);
            _energyMechanics = new EnergyMechanics(
                _character.Energy,
                _character.JumpRequest,
                _character.RoofJumpRequest,
                _character.SuperJumpRequest,
                _character.SuperRoofJumpRequest);
            _uiGameOverMechanics = new UiGameOverMechanics(_uiManager, _gameManager, _character);
            _uiLoseModalMechanics = new UiLoseModalMechanics(_uiManager, _gameManager, _character);
            _uiWinModalMechanics = new UiWinModalMechanics(_uiManager, _gameManager);
            _keyboardMechanics = new KeyboardMechanics(_character, _uiManager, _gameManager);

            _uiManager.SubscribeToEvents();
            _energyMechanics.Subscribe();
            _uiGameScreenMechanics.Subscribe();
            _uiGameOverMechanics.Subscribe();

            await _uiManager.LoadScreenAsync(ScreenEnum.GameScreen);

            _isInitialized = true;
        }

        private void Update()
        {
            if(!_isInitialized) return;

            _uiGameScreenMechanics.OnUpdate();
            _keyboardMechanics.OnUpdate();
            _energyMechanics.OnUpdate(Time.deltaTime);
        }

        private void OnDisable()
        {
            _uiManager.UnsubscribeFromEvents();
            _energyMechanics.Unsubscribe();
            _uiGameScreenMechanics.Unsubscribe();
            _uiGameOverMechanics.Unsubscribe();
        }
    }
}
