using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Bot.StrategiesNew.JumpOver;
using Assets.Scripts.Bot.StrategiesNew.JumpOn;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpOver;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpOn;
using Assets.Scripts.Bot.StrategiesNew.SwitchLane;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Оркестрирует perception, planning и execution бота в рантайме.
    /// </summary>
    public sealed class RuntimeBotController : MonoBehaviour, Listeners.IGameLateUpdateListener
    {
        private const float _initRetryInterval = 0.5f;
        private const string _hostObjectName = "[Bot]";

        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();

        private PlanExecutorNew _executor;
        private Hamster _hamster;
        private GameManager _gameManager;
        private GameManager _registeredGameManager;
        private PlanBuilderNew _planBuilder;
        private RuntimeBotEventTracker _eventTracker;
        private float _nextInitRetryTime;

        public bool IsEnabled { get; private set; } = true;
        public bool IsInitialized => _hamster != null && _gameManager != null;
        public WorldSnapshot LastSnapshot { get; private set; }
        public BotPlan CurrentPlan => _executor?.CurrentPlan ?? BotPlan.Empty();

        /// <summary>
        /// Гарантирует, что после загрузки сцены в runtime существует ровно один контроллер бота.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include) != null)
                return;

            GameObject host = GameObject.Find(_hostObjectName);
            if (host == null)
                host = new GameObject(_hostObjectName);

            host.AddComponent<RuntimeBotController>();
        }

        /// <summary>
        /// Переключает бота между включённым и выключенным состояниями.
        /// </summary>
        public void ToggleEnabled()
        {
            if (IsEnabled)
            {
                Disable();
                return;
            }

            Enable();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            BotAnimationTravelProvider.Reset();

            // Подключаем стратегии
            IReadOnlyList<IPlanningStrategyNew> strategies = new IPlanningStrategyNew[]
            {
                new SwitchLaneStrategyNew(),
                new JumpOverStrategyNew(),
                new SuperJumpOverStrategyNew(),
                new JumpOnStrategyNew(),
                new SuperJumpOnStrategyNew()
            };

            // и прочие компоненты
            _executor = new PlanExecutorNew(strategies);
            _planBuilder = new PlanBuilderNew(
                new ActionGeneratorNew(strategies),
                new TransitionSimulatorNew(strategies),
                new PlanEvaluator(),
                new RetainedActionRevalidatorNew(strategies),
                new ActionInProgressProjectorNew(strategies));
        }

        /// <summary>
        /// Подключает бота к game loop, когда runtime-зависимости становятся доступны.
        /// </summary>
        private void Update()
        {
            if (!IsEnabled)
                return;

            if (!IsInitialized || _registeredGameManager == null)
                TryResolveRuntimeDependencies();
        }

        /// <summary>
        /// Выполняет один кадр цикла бота внутри детерминированного game loop после update-движения мира.
        /// </summary>
        public void OnLateUpdate(float deltaTime)
        {
            if (!IsEnabled)
                return;

            if (!IsReadyForTick())
                return;

            TickBot();
        }

        private void OnDestroy()
        {
            UnregisterFromGameManager();
            _eventTracker?.Dispose();
        }

        private void Enable()
        {
            IsEnabled = true;
            if (!IsInitialized)
                TryResolveRuntimeDependencies();
        }

        private void Disable()
        {
            IsEnabled = false;
            LastSnapshot = null;
            _executor?.Clear();
        }

        /// <summary>
        /// Выполняет шаг бота: обновляет восприятие, продвигает текущий план и при необходимости активирует новый.
        /// </summary>
        private void TickBot()
        {
            if (_executor == null || _planBuilder == null)
                return;

            // Сначала снимаем snapshot для текущего execution-тика.
            LastSnapshot = _snapshotBuilder.Build(_hamster);
            bool executionChanged = _executor.Tick(_hamster);

            // Переснимаем snapshot только после фактического execution-перехода.
            // В обычных кадрах без fire/complete/cancel исходный snapshot остаётся актуальным для replanning.
            if (executionChanged)
                LastSnapshot = _snapshotBuilder.Build(_hamster);

            TrySetNewPlan();
        }

        /// <summary>
        /// Держит контроллер в ожидании, пока не найдены scene-зависимости и пока gameplay не перейдёт в тикаемое состояние.
        /// </summary>
        private bool IsReadyForTick()
        {
            if (!IsInitialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryResolveRuntimeDependencies();
                    _nextInitRetryTime = Time.time + _initRetryInterval;
                }

                return false;
            }

            return _gameManager.State == GameState.PLAYING
                && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
        }

        private void TrySetNewPlan()
        {
            if (_executor == null || _planBuilder == null)
                return;

            BotPlan plan = _planBuilder.Build(
                LastSnapshot,
                _executor.CurrentPlan,
                _executor.IsActionInProgress);

            if (!plan.HasActions || plan.IsEquivalentTo(CurrentPlan))
                return;

            _executor.SetPlan(plan);
            LogPlanActivation(plan);
        }

        /// <summary>
        /// Пишет краткую диагностическую строку для только что активированного плана.
        /// </summary>
        private static void LogPlanActivation(BotPlan plan)
        {
            DebugManager.DiagLogVerbose(
                $"[Bot PLAN] actions={plan.Actions.Count} " +
                $"score={plan.Score:F2} boundaryX={plan.CommittedBoundaryX:F2} " +
                $"head={plan.Actions[0].Description}");
        }

        /// <summary>
        /// Находит scene-зависимости контроллера и лениво подключает трекер runtime-событий.
        /// </summary>
        private void TryResolveRuntimeDependencies()
        {
            _hamster = FindAnyObjectByType<Hamster>(FindObjectsInactive.Exclude);
            _gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Exclude);

            if (!IsInitialized)
                return;

            RegisterWithGameManager(_gameManager);

            if (_eventTracker == null)
                _eventTracker = new RuntimeBotEventTracker(_hamster, _gameManager);
        }

        private void RegisterWithGameManager(GameManager gameManager)
        {
            if (gameManager == null || ReferenceEquals(_registeredGameManager, gameManager))
                return;

            UnregisterFromGameManager();
            gameManager.AddListener(this);
            _registeredGameManager = gameManager;
        }

        private void UnregisterFromGameManager()
        {
            if (_registeredGameManager == null)
                return;

            _registeredGameManager.RemoveListener(this);
            _registeredGameManager = null;
        }
    }
}
