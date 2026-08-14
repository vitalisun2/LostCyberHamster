using System;
using System.Threading.Tasks;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
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
        private Label _runScore;
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
        private DoubleJumpDetector _doubleJumpDetector = new();
        protected override ScreenEnum _screenAssetName => ScreenEnum.GameScreen;

        public GameScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        private void OnClickBtnPause(PointerDownEvent evt)
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

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
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            _tapAction?.Invoke();
        }

        private void OnClickUltra(PointerDownEvent evt)
        {
            TryActivateUltra();
        }

        private void OnClickJump(PointerDownEvent evt)
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

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
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            _buyEnergyAction?.Invoke();
        }

        private void OnClickBuyUltra(PointerDownEvent evt)
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            _buyUltraAction?.Invoke();
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
            _runScore = _contentRoot.Q<Label>("run-score");
            _hamsterState = _contentRoot.Q<Label>("hamster-state-debug-label");
            _hamsterState ??= _contentRoot.Q<Label>("debug-game");
            _jumpButton = _contentRoot.Q<Button>("btn_jump");
            _buyEnergyButton = _contentRoot.Q<Button>("btn_buy_energy");
            _buyUltraButton = _contentRoot.Q<Button>("btn_buy_ulta");
            _ultraButton = _contentRoot.Q<Button>("btn_ultra");
            _tapArea = _contentRoot.Q<VisualElement>("tap");

            ClearBackground();
            HideDebugStateInPlayerBuild();
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

        /// <summary>
        /// Показывает неотрицательные очки забега и уменьшает шрифт для длинных значений.
        /// </summary>
        public void SetRunScore(int score)
        {
            if (_runScore == null)
            {
                return;
            }

            // Обновляем значение и сохраняем его в доступной ширине верхней полосы.
            var scoreText = Math.Max(0, score).ToString();
            _runScore.text = scoreText;
            _runScore.style.fontSize = scoreText.Length switch
            {
                <= 4 => 52,
                <= 7 => 44,
                _ => 36
            };
        }

        public void SetHamsterState(string state)
        {
            if (_hamsterState != null)
            {
                _hamsterState.text = state;
            }
        }

        private void HideDebugStateInPlayerBuild()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (_hamsterState != null)
            {
                _hamsterState.style.display = DisplayStyle.None;
            }
#endif
        }

        public void SetUltraControlsVisible(bool visible)
        {
            SetElementVisible(_ultraButton, visible);
            SetElementVisible(_buyUltraButton, visible);

            if (!visible)
            {
                _ultraButton?.SetEnabled(false);
            }

            _buyUltraButton?.SetEnabled(visible);
        }

        public void SetUltraValue(int value)
        {
            if (_ultraButton == null)
            {
                return;
            }

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

        /// <summary>
        /// Активирует суперудар через готовую и доступную кнопку игрового HUD.
        /// </summary>
        public bool TryActivateUltra()
        {
            if (GameplayInputGate.IsBlocked ||
                _ultraButton?.enabledInHierarchy != true ||
                _ultraAction == null)
            {
                return false;
            }

            _ultraAction.Invoke();
            return true;
        }

        private static void SetElementVisible(VisualElement element, bool visible)
        {
            if (element == null)
            {
                return;
            }

            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetJumpAction(Action action)
        {
            _jumpAction = action;
        }

        public void SetSuperJumpAction(Action action)
        {
            _superJumpAction = action;
        }

        public void ResetJumpSequence()
        {
            _doubleJumpDetector.Reset();
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
