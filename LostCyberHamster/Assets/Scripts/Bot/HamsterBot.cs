using System;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Главный оркестратор бота. Вешается на GameObject в сцене.
    /// Каждый кадр: сканирует препятствия → принимает решение → выполняет действие.
    /// Горячие клавиши: F1 — вкл/выкл, F2 — смена режима.
    /// </summary>
    public class HamsterBot : MonoBehaviour
    {
        public static HamsterBot Instance { get; private set; }

        // ──────────────── Inspector ────────────────

        [Title("HamsterBot Settings")]
        [SerializeField] private bool _enabledOnStart;

        [SerializeField, Range(0.02f, 0.2f)]
        [Tooltip("Минимальный интервал между действиями (сек)")]
        private float _actionCooldown = 0.05f;

        [SerializeField, Range(5f, 30f)]
        [Tooltip("Дальность сканирования (мировые единицы)")]
        private float _scanRange = 15f;

        [SerializeField, Range(0.2f, 1.5f)]
        [Tooltip("Окно быстрой реакции (сек): внутри — BotBrain, снаружи — BotPlanner")]
        private float _urgentWindowSec = 0.6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Агрессивность (0=осторожный, 1=агрессивный)")]
        private float _aggressionLevel = 0.7f;

        [Title("Runtime Info"), ReadOnly]
        [ShowInInspector] public bool IsEnabled { get; private set; }

        [ShowInInspector, ReadOnly]
        public BotMode CurrentMode { get; private set; } = BotMode.Play;

        [ShowInInspector, ReadOnly]
        private string _lastDecisionText = "—";

        [ShowInInspector, ReadOnly]
        private int _actionsExecuted;

        [ShowInInspector, ReadOnly]
        private int _framesAlive;

        // ──────────────── Internals ────────────────

        private Hamster _hamster;
        private BotThreatScanner _scanner;
        private BotBrain _brain;
        private BotLogger _logger;

        private float _lastActionTime;
        private bool _initialized;

        // ──────────────── Lifecycle ────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_enabledOnStart)
                TryInitAndEnable();
        }

        private void Update()
        {
            if (!IsEnabled || !_initialized) return;
            if (_hamster == null || _hamster.HamsterState.Value == HamsterStateEnum.Dead)
                return;

            _framesAlive++;

            if (Time.time - _lastActionTime < _actionCooldown)
                return;

            _scanner.Scan(_hamster, _scanRange);

            var decision = _brain.Evaluate(
                _hamster,
                _scanner.CurrentLaneThreats,
                _scanner.OtherLaneThreats);

            if (decision.Action != BotAction.None)
            {
                ExecuteAction(decision);
                _lastActionTime = Time.time;
            }

            _lastDecisionText = $"{decision.Action}: {decision.Reason}";
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            _logger?.Dispose();
        }

        // ──────────────── Public API ────────────────

        /// <summary>
        /// Включить/выключить бота (F1).
        /// </summary>
        public void ToggleEnabled()
        {
            if (IsEnabled)
                Disable();
            else
                TryInitAndEnable();
        }

        /// <summary>
        /// Переключить режим: Play → Test → Analytics → Play (F2).
        /// </summary>
        public void CycleMode()
        {
            CurrentMode = CurrentMode switch
            {
                BotMode.Play => BotMode.Test,
                BotMode.Test => BotMode.Analytics,
                BotMode.Analytics => BotMode.Play,
                _ => BotMode.Play
            };
            DebugManager.DiagLog($"[HamsterBot] Mode switched to {CurrentMode}");
        }

        // ──────────────── Init / Enable / Disable ────────────────

        private void TryInitAndEnable()
        {
            if (!_initialized)
            {
                _hamster = FindObjectOfType<Hamster>();
                if (_hamster == null)
                {
                    Debug.LogWarning("[HamsterBot] Hamster not found in scene. Bot disabled.");
                    return;
                }

                _scanner = new BotThreatScanner();
                _brain = new BotBrain(
                    reactionWindowSec: _urgentWindowSec,
                    aggressionLevel: _aggressionLevel);
                _logger = new BotLogger();

                _initialized = true;
                DebugManager.DiagLog("[HamsterBot] Initialized successfully.");
            }

            IsEnabled = true;
            _lastActionTime = Time.time;
            _actionsExecuted = 0;
            _framesAlive = 0;

            _logger?.OnBotEnabled(CurrentMode);
            SubscribeToGameEvents();

            DebugManager.DiagLog($"[HamsterBot] ENABLED in {CurrentMode} mode.");
        }

        private void Disable()
        {
            IsEnabled = false;
            UnsubscribeFromGameEvents();
            _logger?.OnBotDisabled(_framesAlive, _actionsExecuted);
            DebugManager.DiagLog("[HamsterBot] DISABLED.");
        }

        // ──────────────── Action Execution ────────────────

        private void ExecuteAction(BotDecision decision)
        {
            switch (decision.Action)
            {
                case BotAction.Jump:
                    ExecuteJump();
                    break;

                case BotAction.SuperJump:
                    _hamster.SuperJumpRequest.Invoke();
                    break;

                case BotAction.RoofJump:
                    _hamster.RoofJumpRequest.Invoke();
                    break;

                case BotAction.SuperRoofJump:
                    _hamster.SuperRoofJumpRequest.Invoke();
                    break;

                case BotAction.SwitchLane:
                    _hamster.TapRequest.Invoke();
                    break;

                case BotAction.UseUlta:
                    _hamster.UltaEvent.Invoke();
                    break;
            }

            _actionsExecuted++;
            _logger?.LogAction(decision, _hamster);
            DebugManager.DiagLog(
                $"[HamsterBot] Action #{_actionsExecuted}: {decision.Action} | {decision.Reason} " +
                $"| conf={decision.Confidence:F2} state={_hamster.HamsterState.Value}");
        }

        /// <summary>
        /// Прыжок: на крыше — RoofJump, на земле — Jump.
        /// </summary>
        private void ExecuteJump()
        {
            if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                _hamster.RoofJumpRequest.Invoke();
            else
                _hamster.JumpRequest.Invoke();
        }

        // ──────────────── Game Events (для логгера) ────────────────

        private void SubscribeToGameEvents()
        {
            GameEventsManager.OnObstacleJumpedOver += OnObstacleJumpedOver;
            GameEventsManager.OnObstacleJumpedOn += OnObstacleJumpedOn;
            GameEventsManager.OnCoinCollected += OnCoinCollected;
            GameEventsManager.OnLivesLost += OnLivesLost;
            GameEventsManager.OnLivesAdded += OnLivesAdded;
            GameEventsManager.OnEnergyAdded += OnEnergyAdded;
            GameEventsManager.OnEnergySpent += OnEnergySpent;
            GameEventsManager.OnUltaUsed += OnUltaUsed;
            GameEventsManager.OnObstacleCollision += OnObstacleCollision;
        }

        private void UnsubscribeFromGameEvents()
        {
            GameEventsManager.OnObstacleJumpedOver -= OnObstacleJumpedOver;
            GameEventsManager.OnObstacleJumpedOn -= OnObstacleJumpedOn;
            GameEventsManager.OnCoinCollected -= OnCoinCollected;
            GameEventsManager.OnLivesLost -= OnLivesLost;
            GameEventsManager.OnLivesAdded -= OnLivesAdded;
            GameEventsManager.OnEnergyAdded -= OnEnergyAdded;
            GameEventsManager.OnEnergySpent -= OnEnergySpent;
            GameEventsManager.OnUltaUsed -= OnUltaUsed;
            GameEventsManager.OnObstacleCollision -= OnObstacleCollision;
        }

        private void OnObstacleJumpedOver(string name) => _logger?.LogEvent("JumpedOver", name);
        private void OnObstacleJumpedOn(string name) => _logger?.LogEvent("JumpedOn", name);
        private void OnCoinCollected(int value) => _logger?.LogEvent("CoinCollected", value.ToString());
        private void OnLivesLost(int amount) => _logger?.LogEvent("LivesLost", amount.ToString());
        private void OnLivesAdded(int amount) => _logger?.LogEvent("LivesAdded", amount.ToString());
        private void OnEnergyAdded(int amount) => _logger?.LogEvent("EnergyAdded", amount.ToString());
        private void OnEnergySpent(int amount) => _logger?.LogEvent("EnergySpent", amount.ToString());
        private void OnUltaUsed() => _logger?.LogEvent("UltaUsed", "");
        private void OnObstacleCollision() => _logger?.LogEvent("Collision", "");
    }
}
