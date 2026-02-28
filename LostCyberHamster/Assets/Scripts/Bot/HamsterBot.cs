using System;
using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine;
using GameManagement;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Bot.Learning;
using Assets.Scripts.System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Title("Auto-Play")]
        [SerializeField]
        [Tooltip("Авто-рестарт уровня при проигрыше")]
        private bool _autoRestartOnDeath = true;

        [SerializeField]
        [Tooltip("Авто-переход на следующий уровень при победе")]
        private bool _autoNextOnWin = true;

        [SerializeField, Range(1f, 5f)]
        [Tooltip("Задержка перед авто-действием (сек)")]
        private float _autoActionDelay = 2f;

        [Title("Runtime Info"), ReadOnly]
        [ShowInInspector] public bool IsEnabled { get; private set; }

        [ShowInInspector, ReadOnly]
        public BotMode CurrentMode { get; private set; } = BotMode.Play;

        [ShowInInspector, ReadOnly]
        public BotPlayStyle CurrentPlayStyle { get; private set; } = BotPlayStyle.Survival;

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
        private BotResourceManager _resourceManager;
        private LearningOrchestrator _learningOrchestrator;

        private float _lastActionTime;
        private bool _initialized;
        private GameManager _gameManager;

        // ──────────────── Lifecycle ────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (_enabledOnStart)
                TryInitAndEnable();
        }

        /// <summary>
        /// При перезагрузке сцены (авто-рестарт/авто-next) нужно переинициализировать ссылки.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsEnabled) return;

            // Сброс — старые ссылки мертвы после reload
            _initialized = false;
            _hamster = null;
            _gameManager = null;

            // Даём сцене время инициализироваться, потом ре-инициализируем бота
            StartCoroutine(ReinitAfterSceneLoad());
        }

        private IEnumerator ReinitAfterSceneLoad()
        {
            // Ждём 2 секунды — сцена, Zenject, LevelController должны инициализироваться
            yield return new WaitForSeconds(2f);

            if (!IsEnabled) yield break;

            TryInitAndEnable();
            DebugManager.DiagLog($"[HamsterBot] Re-initialized after scene load. Level: {GetCurrentLevelName()}");
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
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (Instance == this)
                Instance = null;

            _logger?.Dispose();
            _learningOrchestrator?.Dispose();
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

        /// <summary>
        /// Включить/выключить режим обучения (F4).
        /// </summary>
        public void ToggleTraining()
        {
            _learningOrchestrator?.ToggleTraining();
            DebugManager.DiagLog($"[HamsterBot] Training mode: {IsTrainingMode}");
        }

        /// <summary>Сбросить геном к пресету для текущего уровня/стиля.</summary>
        public void ResetGenome()
        {
            _learningOrchestrator?.ResetGenome(CurrentPlayStyle, GetCurrentLevelName());
        }

        /// <summary>Включён ли режим обучения.</summary>
        public bool IsTrainingMode => _learningOrchestrator?.IsTrainingMode ?? false;

        /// <summary>Оркестратор обучения (для UI).</summary>
        public LearningOrchestrator LearningOrchestrator => _learningOrchestrator;

        /// <summary>
        /// Переключить стиль игры: Survival → ThreeStars → ... → GodMode → Survival (F3).
        /// </summary>
        public void CyclePlayStyle()
        {
            CurrentPlayStyle = CurrentPlayStyle switch
            {
                BotPlayStyle.Survival => BotPlayStyle.ThreeStars,
                BotPlayStyle.ThreeStars => BotPlayStyle.BonusHunter,
                BotPlayStyle.BonusHunter => BotPlayStyle.Perfectionist,
                BotPlayStyle.Perfectionist => BotPlayStyle.UltaMaster,
                BotPlayStyle.UltaMaster => BotPlayStyle.GodMode,
                BotPlayStyle.GodMode => BotPlayStyle.Survival,
                _ => BotPlayStyle.Survival
            };

            var config = BotPlayStylePresets.Get(CurrentPlayStyle);
            _brain?.ApplyConfig(config);
            DebugManager.DiagLog($"[HamsterBot] PlayStyle switched to {CurrentPlayStyle}");
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
                _resourceManager = new BotResourceManager();

                // Оркестратор обучения переживает scene reload (хранит FailReasons для мутаций)
                if (_learningOrchestrator == null)
                    _learningOrchestrator = new LearningOrchestrator();

                var styleConfig = _learningOrchestrator.InitForLevel(
                    _hamster, CurrentPlayStyle, GetCurrentLevelName());
                _brain = new BotBrain(styleConfig, _resourceManager);
                _logger = new BotLogger();

                // GameManager для отслеживания конца уровня
                if (LevelController.Instance != null && LevelController.Instance.LevelData != null)
                    _gameManager = LevelController.Instance.LevelData.GameManager;

                _initialized = true;
                DebugManager.DiagLog("[HamsterBot] Initialized successfully.");
            }

            IsEnabled = true;
            _lastActionTime = Time.time;
            _actionsExecuted = 0;
            _framesAlive = 0;

            _logger?.OnBotEnabled(CurrentMode, GetCurrentLevelName(), CurrentPlayStyle);
            SubscribeToGameEvents();

            // Отписаться перед подпиской на OnFinish (предотвращаем дубли)
            if (_gameManager != null)
                _gameManager.OnFinish -= OnGameFinished;
            if (_gameManager != null)
                _gameManager.OnFinish += OnGameFinished;

            DebugManager.DiagLog($"[HamsterBot] ENABLED in {CurrentMode} mode.");
        }

        private void Disable()
        {
            IsEnabled = false;
            UnsubscribeFromGameEvents();

            if (_gameManager != null)
                _gameManager.OnFinish -= OnGameFinished;

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

                case BotAction.BuyEnergy:
                    _resourceManager?.BuyEnergy(_hamster);
                    break;

                case BotAction.BuyUlta:
                    _resourceManager?.BuyUlta(_hamster);
                    break;
            }

            _actionsExecuted++;
            _learningOrchestrator?.TrackAction(decision.Action);
            _logger?.LogAction(decision, _hamster, _scanner.CurrentLaneThreats, _scanner.OtherLaneThreats);
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
            // Сначала отписываемся, чтобы не накапливать дубли при scene reload
            UnsubscribeFromGameEvents();

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

        // ──────────────── Auto-Restart / Auto-Next ────────────────

        private void OnGameFinished()
        {
            if (!IsEnabled) return;

            bool lost = _hamster != null && _hamster.Lives.Value <= 0;
            string outcome = lost ? "LOST" : "WON";
            _logger?.LogEvent("GameFinished", $"{outcome} | level={GetCurrentLevelName()}");
            _learningOrchestrator?.OnGameFinished(!lost);
            DebugManager.DiagLog($"[HamsterBot] Game finished: {outcome}");

            if (lost && _autoRestartOnDeath)
            {
                StartCoroutine(AutoRestartCoroutine());
            }
            else if (!lost && _autoNextOnWin)
            {
                StartCoroutine(AutoNextLevelCoroutine());
            }
        }

        private IEnumerator AutoRestartCoroutine()
        {
            DebugManager.DiagLog($"[HamsterBot] Auto-restart in {_autoActionDelay}s...");
            yield return new WaitForSecondsRealtime(_autoActionDelay);

            if (!IsEnabled) yield break;

            _logger?.LogEvent("AutoRestart", GetCurrentLevelName());
            DebugManager.DiagLog("[HamsterBot] Auto-restarting level.");
            LevelController.Instance.Replay();
        }

        private IEnumerator AutoNextLevelCoroutine()
        {
            DebugManager.DiagLog($"[HamsterBot] Auto-next level in {_autoActionDelay}s...");
            yield return new WaitForSecondsRealtime(_autoActionDelay);

            if (!IsEnabled) yield break;

            _logger?.LogEvent("AutoNextLevel", GetCurrentLevelName());
            DebugManager.DiagLog("[HamsterBot] Auto-advancing to next level.");
            LevelController.Instance.PlayNextLevel();
        }

        // ──────────────── Helpers ────────────────

        private static string GetCurrentLevelName()
        {
            try
            {
                return GameDataManager.PlayerData?.CurrentLevel ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
