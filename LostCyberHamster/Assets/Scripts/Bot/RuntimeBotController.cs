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
using Assets.Scripts.Bot.StrategiesNew.JumpFromRoof;
using Assets.Scripts.Bot.StrategiesNew.JumpFromRoofOnRoof;
using Assets.Scripts.Bot.StrategiesNew.JumpOver;
using Assets.Scripts.Bot.StrategiesNew.JumpOn;
using Assets.Scripts.Bot.StrategiesNew.JumpOnRoof;
using Assets.Scripts.Bot.StrategiesNew.PassiveRoofExit;
using Assets.Scripts.Bot.StrategiesNew.RoofJumpOver;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpFromRoof;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpFromRoofOnRoof;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpOver;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpOn;
using Assets.Scripts.Bot.StrategiesNew.SuperJumpOnRoof;
using Assets.Scripts.Bot.StrategiesNew.SuperRoofJumpOver;
using Assets.Scripts.Bot.StrategiesNew.SwitchLane;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Оркестрирует perception, planning и execution бота в рантайме.
    /// </summary>
    public sealed class RuntimeBotController : MonoBehaviour, Listeners.IGameLateUpdateListener
    {
        /// <summary>
        /// Интервал повторной попытки поиска scene-зависимостей.
        /// </summary>
        private const float _initRetryInterval = 0.5f;

        /// <summary>
        /// Имя runtime GameObject, на который подключается bot controller.
        /// </summary>
        private const string _hostObjectName = "[Bot]";

        /// <summary>
        /// Builder runtime snapshot мира вокруг хомяка.
        /// </summary>
        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();

        /// <summary>
        /// Executor текущего bot plan.
        /// </summary>
        private PlanExecutorNew _executor;

        /// <summary>
        /// Runtime hamster instance в текущей scene.
        /// </summary>
        private Hamster _hamster;

        /// <summary>
        /// Runtime game manager текущей scene.
        /// </summary>
        private GameManager _gameManager;

        /// <summary>
        /// Game manager, в который controller уже зарегистрирован как listener.
        /// </summary>
        private GameManager _registeredGameManager;

        /// <summary>
        /// Builder нового bot plan по snapshot и текущему execution state.
        /// </summary>
        private PlanBuilderNew _planBuilder;

        /// <summary>
        /// Tracker runtime-событий бота для диагностики.
        /// </summary>
        private RuntimeBotEventTracker _eventTracker;

        /// <summary>
        /// Runtime-время следующей попытки инициализации.
        /// </summary>
        private float _nextInitRetryTime;

        /// <summary>
        /// Признак включенного bot controller.
        /// </summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// Признак найденных runtime-зависимостей.
        /// </summary>
        public bool IsInitialized => _hamster != null && _gameManager != null;

        /// <summary>
        /// Последний snapshot, построенный controller-ом.
        /// </summary>
        public WorldSnapshot LastSnapshot { get; private set; }

        /// <summary>
        /// Текущий plan executor-а или пустой plan.
        /// </summary>
        public BotPlan CurrentPlan => _executor?.CurrentPlan ?? BotPlan.Empty();

        /// <summary>
        /// Гарантирует, что после загрузки сцены в runtime существует ровно один контроллер бота.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            // Проверяет, что controller уже есть в scene.
            if (FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include) != null)
                return;

            // Находит или создает host object.
            GameObject host = GameObject.Find(_hostObjectName);
            if (host == null)
                host = new GameObject(_hostObjectName);

            // Подключает controller к host object.
            host.AddComponent<RuntimeBotController>();
        }

        /// <summary>
        /// Переключает бота между включённым и выключенным состояниями.
        /// </summary>
        public void ToggleEnabled()
        {
            // Выключает controller, если он активен.
            if (IsEnabled)
            {
                Disable();
                return;
            }

            // Включает controller, если он неактивен.
            Enable();
        }

        /// <summary>
        /// Инициализирует persistent controller и собирает role-based planning dependencies.
        /// </summary>
        private void Awake()
        {
            // Подготавливает persistent controller.
            DontDestroyOnLoad(gameObject);
            BotAnimationTravelProvider.Reset();

            // Подключаем стратегии
            IReadOnlyList<IPlanningStrategyNew> strategies = new IPlanningStrategyNew[]
            {
                new SwitchLaneStrategyNew(),
                new JumpOverStrategyNew(),
                new SuperJumpOverStrategyNew(),
                new JumpOnStrategyNew(),
                new SuperJumpOnStrategyNew(),
                new JumpOnRoofStrategyNew(),
                new SuperJumpOnRoofStrategyNew(),
                new PassiveRoofExitStrategyNew(),
                new JumpFromRoofStrategyNew(),
                new SuperJumpFromRoofStrategyNew(),
                new JumpFromRoofOnRoofStrategyNew(),
                new SuperJumpFromRoofOnRoofStrategyNew(),
                new RoofJumpOverStrategyNew(),
                new SuperRoofJumpOverStrategyNew()
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
            // Игнорирует update в выключенном состоянии.
            if (!IsEnabled)
                return;

            // Пытается найти runtime-зависимости.
            if (!IsInitialized || _registeredGameManager == null)
                TryResolveRuntimeDependencies();
        }

        /// <summary>
        /// Выполняет один кадр цикла бота внутри детерминированного game loop после update-движения мира.
        /// </summary>
        public void OnLateUpdate(float deltaTime)
        {
            // Игнорирует tick в выключенном состоянии.
            if (!IsEnabled)
                return;

            // Проверяет готовность к bot tick.
            if (!IsReadyForTick())
                return;

            // Выполняет bot tick после движения мира.
            TickBot();
        }

        /// <summary>
        /// Освобождает регистрацию в game loop и runtime tracker.
        /// </summary>
        private void OnDestroy()
        {
            // Отписывает controller от scene-зависимостей.
            UnregisterFromGameManager();
            _eventTracker?.Dispose();
        }

        /// <summary>
        /// Включает bot controller и пытается восстановить runtime-зависимости.
        /// </summary>
        private void Enable()
        {
            // Переводит controller в активное состояние.
            IsEnabled = true;
            if (!IsInitialized)
                TryResolveRuntimeDependencies();
        }

        /// <summary>
        /// Выключает bot controller и сбрасывает текущий execution state.
        /// </summary>
        private void Disable()
        {
            // Очищает runtime state бота.
            IsEnabled = false;
            LastSnapshot = null;
            _executor?.Clear();
        }

        /// <summary>
        /// Выполняет шаг бота: обновляет восприятие, продвигает текущий план и при необходимости активирует новый.
        /// </summary>
        private void TickBot()
        {
            // Проверяет готовность planning компонентов.
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
            // Ожидает scene-зависимости.
            if (!IsInitialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryResolveRuntimeDependencies();
                    _nextInitRetryTime = Time.time + _initRetryInterval;
                }

                return false;
            }

            // Разрешает tick только во время gameplay.
            return _gameManager.State == GameState.PLAYING
                && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
        }

        /// <summary>
        /// Строит и активирует новый plan, если он отличается от текущего.
        /// </summary>
        private void TrySetNewPlan()
        {
            // Проверяет готовность planning компонентов.
            if (_executor == null || _planBuilder == null)
                return;

            // Строит candidate plan по текущему snapshot.
            BotPlan plan = _planBuilder.Build(
                LastSnapshot,
                _executor.CurrentPlan,
                _executor.IsActionInProgress);

            // Отбрасывает пустой или эквивалентный plan.
            if (!plan.HasActions || plan.IsEquivalentTo(CurrentPlan))
                return;

            // Активирует новый plan.
            _executor.SetPlan(plan);
            LogPlanActivation(plan);
        }

        /// <summary>
        /// Пишет краткую диагностическую строку для только что активированного плана.
        /// </summary>
        private static void LogPlanActivation(BotPlan plan)
        {
            // Пишет компактную строку о выбранном plan.
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
            // Ищет runtime-зависимости в scene.
            _hamster = FindAnyObjectByType<Hamster>(FindObjectsInactive.Exclude);
            _gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Exclude);

            // Ждет полный набор зависимостей.
            if (!IsInitialized)
                return;

            // Подключает controller к game loop.
            RegisterWithGameManager(_gameManager);

            // Лениво создает tracker runtime-событий.
            if (_eventTracker == null)
                _eventTracker = new RuntimeBotEventTracker(_hamster, _gameManager);
        }

        /// <summary>
        /// Регистрирует controller в переданном game manager.
        /// </summary>
        private void RegisterWithGameManager(GameManager gameManager)
        {
            // Проверяет, нужна ли новая регистрация.
            if (gameManager == null || ReferenceEquals(_registeredGameManager, gameManager))
                return;

            // Перерегистрирует controller в актуальный game manager.
            UnregisterFromGameManager();
            gameManager.AddListener(this);
            _registeredGameManager = gameManager;
        }

        /// <summary>
        /// Отписывает controller от текущего game manager.
        /// </summary>
        private void UnregisterFromGameManager()
        {
            // Проверяет наличие активной регистрации.
            if (_registeredGameManager == null)
                return;

            // Удаляет listener из game manager.
            _registeredGameManager.RemoveListener(this);
            _registeredGameManager = null;
        }
    }
}
