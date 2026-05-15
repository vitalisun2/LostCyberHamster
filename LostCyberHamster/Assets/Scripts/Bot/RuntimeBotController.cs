using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Execution;
using Assets.Scripts.Bot.Strategies.JumpFromRoof;
using Assets.Scripts.Bot.Strategies.JumpOnRoof;
using Assets.Scripts.Bot.Strategies.JumpOver;
using Assets.Scripts.Bot.Strategies.RoofJumpOver;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.SuperRoofJumpOver;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOnRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOver;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Оркестрирует perception, planning и execution бота в рантайме.
    /// </summary>
    public sealed class RuntimeBotController : MonoBehaviour
    {
        private const float _initRetryInterval = 0.5f;
        private const string _hostObjectName = "[Bot]";

        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();

        private PlanExecutor _executor;
        private Hamster _hamster;
        private GameManager _gameManager;
        private PlanBuilder _planBuilder;
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
            IReadOnlyList<IPlanningStrategy> strategies = CreateStrategies();

            _executor = new PlanExecutor(strategies);
            _planBuilder = new PlanBuilder(
                new ActionGenerator(strategies),
                new TransitionSimulator(strategies),
                new PlanEvaluator(),
                new RetainedActionRevalidator(strategies),
                new ActionInProgressProjector(strategies));
        }

        private static IReadOnlyList<IPlanningStrategy> CreateStrategies()
        {
            return new IPlanningStrategy[]
            {
                new SwitchLaneStrategy(),
                new JumpOverStrategy(),
                new SuperJumpOverStrategy(),
                new JumpOnRoofStrategy(),
                new SuperJumpOnRoofStrategy(),
                new JumpFromRoofStrategy(),
                new SuperJumpFromRoofStrategy(),
                new RoofJumpOverStrategy(),
                new SuperRoofJumpOverStrategy()
            };
        }

        /// <summary>
        /// Выполняет один кадр цикла бота, когда runtime-зависимости готовы и игровой ран активен.
        /// </summary>
        private void Update()
        {
            if (!IsEnabled)
                return;

            if (!IsReadyForTick())
                return;

            TickBot();
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
        }

        private void Enable()
        {
            IsEnabled = true;
            if (!IsInitialized)
                TryResolveRuntimeDependencies();

            DebugManager.DiagLog("[Bot] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            LastSnapshot = null;
            _executor?.Clear();
            DebugManager.DiagLog("[Bot] Disabled");
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
            _executor.Tick(_hamster);

            // Затем обновляем snapshot заново, чтобы replanning видел фактическое post-action состояние.
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

            BotPlan plan = _planBuilder.Build(LastSnapshot, _executor.CurrentPlan, _executor.IsActionInProgress);
            if (!plan.HasActions || plan.IsEquivalentTo(_executor.CurrentPlan))
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

            if (_eventTracker == null)
                _eventTracker = new RuntimeBotEventTracker(_hamster, _gameManager);
        }
    }
}
