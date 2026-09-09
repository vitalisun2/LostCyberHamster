using System;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class GameScreenController : ScreenController
    {
        private const float RunCounterScale = 0.8f;
        private const float RunCounterIconInset = 44f;
        private const float RunCounterPlatePadding = 56f;
        private const float RunCounterDigitWidth = 28f;

        private Button _buttonPause;
        private Energybar _energyBar;
        private Healthbar _healthBar;
        private Label _runScore;
        private Label _runCoins;
        private Label _runCrystals;
        private VisualElement _hudSafeArea;
        private int _runScoreValue;
        private int _runCoinsValue;
        private int _runCrystalsValue;
        private Label _hamsterState;
        private Button _jumpButton;
        private Button _buyEnergyButton;
        private Button _buyUltraButton;
        private Button _ultraButton;
        private Label _ultraChargeValue;

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
            evt.StopImmediatePropagation();
            RequestPause();
        }

        /// <summary>Передаёт паузу отдельному обработчику, сохраняя приоритет блокировки рекламы.</summary>
        internal void RequestPause()
        {
            if (!UiInputBlock.IsBlocked)
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
            _hudSafeArea?.RegisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
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
            _hudSafeArea?.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
        }

        protected override void BindView()
        {
            // Связываем элементы нового дерева экрана.
            _buttonPause = _contentRoot.Q<Button>("btn_pause");
            _energyBar = _contentRoot.Q<Energybar>();
            _healthBar = _contentRoot.Q<Healthbar>();
            _runScore = _contentRoot.Q<Label>("run-score");
            _runCoins = _contentRoot.Q<Label>("run-coins");
            _runCrystals = _contentRoot.Q<Label>("run-crystals");
            _hudSafeArea = _contentRoot.Q<VisualElement>("hud-safe-area");
            _hamsterState = _contentRoot.Q<Label>("hamster-state-debug-label");
            _hamsterState ??= _contentRoot.Q<Label>("debug-game");
            _jumpButton = _contentRoot.Q<Button>("btn_jump");
            _buyEnergyButton = _contentRoot.Q<Button>("btn_buy_energy");
            _buyUltraButton = _contentRoot.Q<Button>("btn_buy_ulta");
            _ultraButton = _contentRoot.Q<Button>("btn_ultra");
            _ultraChargeValue = _contentRoot.Q<Label>("ulta-charge-value");
            _tapArea = _contentRoot.Q<VisualElement>("tap");

            // Готовим существующие элементы управления.
            ClearBackground();
            HideDebugStateInPlayerBuild();
            _doubleJumpDetector.Reset();

            // Восстанавливаем счётчики при создании нового дерева текущего HUD.
            SetRunScore(_runScoreValue);
            SetRunResources(_runCoinsValue, _runCrystalsValue);
            UpdateCounterLayout(_hudSafeArea?.resolvedStyle.width ?? 0);
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
        /// Сохраняет и показывает очки текущего забега.
        /// </summary>
        public void SetRunScore(int score)
        {
            // Обновляем значение и ширину его плашки.
            _runScoreValue = Math.Max(0, score);
            SetRunCounter(_runScore, _runScoreValue);

            // Сохраняем свободное место между соседними блоками HUD.
            UpdateCounterLayout(_hudSafeArea?.resolvedStyle.width ?? 0);
        }

        /// <summary>Сохраняет и показывает валюты, собранные за текущий уровень.</summary>
        public void SetRunResources(int coins, int crystals)
        {
            // Держим данные до появления дерева и между его пересозданиями.
            _runCoinsValue = Math.Max(0, coins);
            _runCrystalsValue = Math.Max(0, crystals);

            // Обновляем обе плашки одним правилом форматирования.
            SetRunCounter(_runCoins, _runCoinsValue);
            SetRunCounter(_runCrystals, _runCrystalsValue);
            UpdateCounterLayout(_hudSafeArea?.resolvedStyle.width ?? 0);
        }

        /// <summary>Выводит полное число и расширяет плашку под количество цифр.</summary>
        private static void SetRunCounter(Label label, int value)
        {
            if (label == null)
            {
                return;
            }

            // Сохраняем общий размер шрифта для коротких и длинных значений.
            label.text = value.ToString();
            label.style.fontSize = StyleKeyword.Null;

            // Резервируем место для иконки, отступов и всех цифр.
            float width = GetRunCounterWidth(value);
            label.parent.style.width = width - RunCounterIconInset;
            label.parent.parent.style.width = width;
        }

        /// <summary>Возвращает ширину счётчика до общего масштабирования HUD.</summary>
        private static float GetRunCounterWidth(int value)
        {
            return RunCounterIconInset + RunCounterPlatePadding
                + Math.Max(4, value.ToString().Length) * RunCounterDigitWidth;
        }

        private void OnHudGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateCounterLayout(evt.newRect.width);
        }

        /// <summary>Переносит счётчики на второй ряд, если сверху не хватает места.</summary>
        private void UpdateCounterLayout(float width)
        {
            if (float.IsNaN(width) || width <= 0)
            {
                return;
            }

            // Суммируем ширины трёх плашек и их боковые отступы по 7 px.
            float countersWidth = (GetRunCounterWidth(_runCoinsValue)
                + GetRunCounterWidth(_runCrystalsValue)
                + GetRunCounterWidth(_runScoreValue) + 42f) * RunCounterScale;

            // Верхний ряд оставляет 420 px энергии и 350 px жизням с паузой.
            _hudSafeArea?.EnableInClassList("game-screen__safe-area--compact",
                width < 1460 || countersWidth > width - 770f);
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

            // Определяем состояние ульты по реальному заряду.
            int clampedValue = Mathf.Clamp(value, 0, 100);
            bool isReady = clampedValue >= 100;

            // Переключаем утверждённый вид и сохраняем блокировку до полного заряда.
            _ultraButton.EnableInClassList("game-control--ready", isReady);
            _ultraButton.SetEnabled(isReady);
            if (_ultraChargeValue != null)
            {
                _ultraChargeValue.text = clampedValue.ToString();
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
