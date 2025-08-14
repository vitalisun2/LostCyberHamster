using System;
using System.Threading.Tasks;
using Assets.Scripts.Common;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class GameScreenController : ScreenController
    {
        private Button _buttonPause;
        private Energybar _energyBar;
        private Healthbar _healthBar;
        private Label _hamsterState;
        private Button _jumpButton;
        private Button _buyEnergyButton;
        private Button _buyUltraButton;
        private Button _ultraButton;

        private Action _jumpAction;
        private Action _superJumpAction;
        private Action _ultraAction;
        private Action _buyEnergyAction;
        private Action _buyUltraAction;

        private VisualElement _tapArea;
        private Action _tapAction;
        private Action _pauseAction;

        private int _currentUltraValue = -1;
        private float _previousEnergy = -1f;
        private int _previousHealth = -1;
        private string _previousHamsterState = null;

        private DoubleJumpDetector _doubleJumpDetector = new();
        protected override ScreenEnum _screenAssetName => ScreenEnum.GameScreen;

        public GameScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        private void OnClickBtnPause(PointerDownEvent evt)
        {
            UIManager.OnModalShow(ScreenEnum.PauseModal);
            _pauseAction?.Invoke();
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonPause?.RegisterCallback<PointerDownEvent>(OnClickBtnPause, TrickleDown.TrickleDown);
            _jumpButton?.RegisterCallback<PointerDownEvent>(OnClickJump, TrickleDown.TrickleDown);
            _ultraButton?.RegisterCallback<PointerDownEvent>(OnClickUltra, TrickleDown.TrickleDown);
            _buyEnergyButton?.RegisterCallback<PointerDownEvent>(OnClickBuyEnergy, TrickleDown.TrickleDown);
            _buyUltraButton?.RegisterCallback<PointerDownEvent>(OnClickBuyUltra, TrickleDown.TrickleDown);
            _tapArea?.RegisterCallback<PointerDownEvent>(OnClickTap, TrickleDown.TrickleDown);
        }

        private void OnClickTap(PointerDownEvent evt)
        {
            _tapAction?.Invoke();
        }

        private void OnClickUltra(PointerDownEvent evt)
        {
            _ultraAction?.Invoke();
        }

        private void OnClickJump(PointerDownEvent evt)
        {
            bool isDoubleJump = _doubleJumpDetector.RegisterJump();

            if (isDoubleJump)
            {
                _superJumpAction?.Invoke();
            }
            else
            {
                _jumpAction?.Invoke();
            }
        }

        private void OnClickBuyEnergy(PointerDownEvent evt)
        {
            _buyEnergyAction?.Invoke();
        }

        private void OnClickBuyUltra(PointerDownEvent evt)
        {
            _buyUltraAction.Invoke();
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonPause?.UnregisterCallback<PointerDownEvent>(OnClickBtnPause, TrickleDown.TrickleDown);
            _jumpButton?.UnregisterCallback<PointerDownEvent>(OnClickJump, TrickleDown.TrickleDown);
            _ultraButton?.UnregisterCallback<PointerDownEvent>(OnClickUltra, TrickleDown.TrickleDown);
            _buyEnergyButton?.UnregisterCallback<PointerDownEvent>(OnClickBuyEnergy, TrickleDown.TrickleDown);
            _buyUltraButton?.UnregisterCallback<PointerDownEvent>(OnClickBuyUltra, TrickleDown.TrickleDown);
            _tapArea?.UnregisterCallback<PointerDownEvent>(OnClickTap, TrickleDown.TrickleDown);
        }

        protected override async Task OnLoadAsync()
        {
            _buttonPause = _contentRoot.Q<Button>("btn_pause");
            _energyBar = _contentRoot.Q<Energybar>();
            _healthBar = _contentRoot.Q<Healthbar>();
            _hamsterState = _contentRoot.Q<Label>("hamster-state-debug-label");
            _jumpButton = _contentRoot.Q<Button>("btn_jump");
            _buyEnergyButton = _contentRoot.Q<Button>("btn_buy_energy");
            _buyUltraButton = _contentRoot.Q<Button>("btn_buy_ulta");
            _ultraButton = _contentRoot.Q<Button>("btn_ultra");
            _tapArea = _contentRoot.Q<VisualElement>("tap");

            ClearBackground();
            _doubleJumpDetector.Reset();
        }

        private void ClearBackground()
        {
            _background.style.backgroundImage = null;
        }

        public void SetEnergy(float energy)
        {
            _energyBar.value = energy;
        }

        public void SetHealth(int health)
        {
            _healthBar.value = health;
        }

        public void SetHamsterState(string state)
        {
            _hamsterState.text = state;
        }

        public void SetUltraValue(int value)
        {
            if (value < 100)
            {
                _ultraButton.text = $"{value}";
                _ultraButton.SetEnabled(false);
            }
            else
            {
                _ultraButton.text = "S";
                _ultraButton.SetEnabled(true);
            }
        }

        public void SetJumpAction(Action action)
        {
            _jumpAction = action;
        }

        public void SetSuperJumpAction(Action action)
        {
            _superJumpAction = action;
        }

        public void SetUltraAction(Action action)
        {
            _ultraAction = action;
        }

        public void SetTapAction(Action action)
        {
            _tapAction = action;
        }

        public void SetPauseAction(Action action)
        {
            _pauseAction = action;
        }

        public void SetBuyEnergyAction(Action action)
        {
            _buyEnergyAction = action;
        }

        public void SetBuyUltraAction(Action action)
        {
            _buyUltraAction = action;
        }
    }
}
