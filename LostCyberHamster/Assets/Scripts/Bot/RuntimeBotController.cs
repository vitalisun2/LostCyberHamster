using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Bot.Strategies.JumpFromRoof;
using Assets.Scripts.Bot.Strategies.JumpFromRoofOnRoof;
using Assets.Scripts.Bot.Strategies.JumpOver;
using Assets.Scripts.Bot.Strategies.JumpOn;
using Assets.Scripts.Bot.Strategies.JumpOnFromRoof;
using Assets.Scripts.Bot.Strategies.JumpOnRoof;
using Assets.Scripts.Bot.Strategies.PassiveAdvance;
using Assets.Scripts.Bot.Strategies.PassiveCollect;
using Assets.Scripts.Bot.Strategies.PassiveRoofExit;
using Assets.Scripts.Bot.Strategies.RoofJumpOver;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOver;
using Assets.Scripts.Bot.Strategies.SuperJumpOn;
using Assets.Scripts.Bot.Strategies.SuperJumpOnFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOnRoof;
using Assets.Scripts.Bot.Strategies.SuperRoofJumpOver;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using UnityEngine;
using RuntimeObstacleSpawner = Assets.Scripts.System.ObstacleSpawner;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Оркестрирует perception, planning и execution бота в рантайме.
    /// </summary>
    public sealed class RuntimeBotController : MonoBehaviour,
        Listeners.IGameStartListener,
        Listeners.IGameLateUpdateListener
    {
        /// <summary>
        /// Интервал повторной попытки поиска scene-зависимостей.
        /// </summary>
        private const float _initRetryInterval = 0.5f;

        /// <summary>
        /// Количество ближайших действий, которые replan не заменяет: текущая голова и следующий action для instant handoff.
        /// </summary>
        private const int _committedPrefixActionCount = 2;

        /// <summary>
        /// Обычное окно спауна без runtime-бота.
        /// </summary>
        private const int _defaultSpawnLookaheadPatterns = 1;

        /// <summary>
        /// Окно спауна для validation-режима runtime-бота.
        /// </summary>
        private const int _botSpawnLookaheadPatterns = 2;

        /// <summary>
        /// Имя runtime GameObject, на который подключается bot controller.
        /// </summary>
        private const string _hostObjectName = "[Bot]";

        [Flags]
        private enum BotReplanReason
        {
            None = 0,
            LevelStart = 1,
            BotEnabled = 2,
            SpawnPattern = 4,
            ActionCompleted = 8,
            ActionCancelled = 16
        }

        /// <summary>
        /// Builder runtime snapshot мира вокруг хомяка.
        /// </summary>
        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();

        /// <summary>
        /// Executor текущего bot plan.
        /// </summary>
        private PlanExecutor _executor;

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
        private PlanBuilder _planBuilder;

        /// <summary>
        /// Проецирует уже исполняемый head-action для replanning хвоста.
        /// </summary>
        private TransitionSimulator _transitionSimulator;

        /// <summary>
        /// Tracker runtime-событий бота для диагностики.
        /// </summary>
        private RuntimeBotEventTracker _eventTracker;

        /// <summary>
        /// Редакторский тестовый сценарий для проверки приоритета life collectible на test_collectables.
        /// </summary>
        private TestCollectablesScriptedLifeLossHook _testCollectablesScriptedLifeLossHook;

        /// <summary>
        /// Runtime-время следующей попытки инициализации.
        /// </summary>
        private float _nextInitRetryTime;

        /// <summary>
        /// Head-action, которая была фактически запущена executor-ом.
        /// </summary>
        private PlannedAction _inProgressHeadAction;

        /// <summary>
        /// Snapshot time момента запуска текущей in-progress head-action.
        /// </summary>
        private float _inProgressHeadFireTime;

        /// <summary>
        /// Признак запрошенной event-driven пересборки плана.
        /// </summary>
        private bool _isReplanRequested;

        /// <summary>
        /// Накопленные причины пересборки плана до ближайшего bot tick.
        /// </summary>
        private BotReplanReason _pendingReplanReasons = BotReplanReason.None;

        /// <summary>
        /// Причины пересборки, которые должны быть применены только со следующего bot tick.
        /// </summary>
        private BotReplanReason _deferredReplanReasons = BotReplanReason.None;

        /// <summary>
        /// Признак уже запрошенного первичного plan для текущего gameplay runtime.
        /// </summary>
        private bool _initialReplanRequestedForCurrentGame;

        /// <summary>
        /// ObstacleSpawner, на событие spawn которого controller сейчас подписан.
        /// </summary>
        private RuntimeObstacleSpawner _subscribedObstacleSpawner;

        /// <summary>
        /// Последняя planning-diagnosis, которая станет dead-end только после потери жизни.
        /// </summary>
        private PlanningDeadEndReport _pendingDeadEndReport;

        /// <summary>
        /// Причины replan-а, при котором была подготовлена последняя planning-diagnosis.
        /// </summary>
        private BotReplanReason _pendingDeadEndReplanReasons = BotReplanReason.None;

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
                Debug.Log("[Bot] OFF");
                return;
            }

            // Включает controller, если он неактивен.
            Enable();
            Debug.Log("[Bot] ON");
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
            IReadOnlyList<IPlanningStrategy> strategies = new IPlanningStrategy[]
            {
                new SwitchLaneStrategy(),
                new PassiveAdvanceStrategy(),
                new JumpOverStrategy(),
                new SuperJumpOverStrategy(),
                new JumpOnStrategy(),
                new SuperJumpOnStrategy(),
                new JumpOnRoofStrategy(),
                new SuperJumpOnRoofStrategy(),
                new PassiveRoofExitStrategy(),
                new PassiveCollectStrategy(),
                new JumpOnFromRoofStrategy(),
                new SuperJumpOnFromRoofStrategy(),
                new JumpFromRoofStrategy(),
                new SuperJumpFromRoofStrategy(),
                new JumpFromRoofOnRoofStrategy(),
                new SuperJumpFromRoofOnRoofStrategy(),
                new RoofJumpOverStrategy(),
                new SuperRoofJumpOverStrategy()
            };

            // и прочие компоненты
            _transitionSimulator = new TransitionSimulator(strategies);
            _executor = new PlanExecutor(strategies);
            _planBuilder = new PlanBuilder(
                new ActionGenerator(strategies),
                _transitionSimulator,
                new PlanEvaluator());
            _testCollectablesScriptedLifeLossHook = new TestCollectablesScriptedLifeLossHook(
                () => _hamster,
                ClearPendingDeadEndReport);

            GameEventsManager.OnLivesLost += OnLivesLost;
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
            {
                TryResolveRuntimeDependencies();
                return;
            }

            // ObstacleSpawner может появиться после регистрации controller-а в GameManager.
            SubscribeToObstacleSpawner(RuntimeObstacleSpawner.Instance);
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
        /// Запрашивает первичную пересборку плана после старта gameplay.
        /// </summary>
        public void OnStart()
        {
            if (!IsEnabled)
                return;

            _testCollectablesScriptedLifeLossHook?.Reset();
            RequestInitialReplan(BotReplanReason.LevelStart);
        }

        /// <summary>
        /// Освобождает регистрацию в game loop и runtime tracker.
        /// </summary>
        private void OnDestroy()
        {
            // Отписывает controller от scene-зависимостей.
            UnsubscribeFromObstacleSpawner();
            UnregisterFromGameManager();
            GameEventsManager.OnLivesLost -= OnLivesLost;
            _eventTracker?.Dispose();
        }

        /// <summary>
        /// Включает bot controller и пытается восстановить runtime-зависимости.
        /// </summary>
        private void Enable()
        {
            // Переводит controller в активное состояние.
            IsEnabled = true;
            ApplySpawnLookaheadToObstacleSpawner();
            RequestInitialReplan(BotReplanReason.BotEnabled);
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
            ApplySpawnLookaheadToObstacleSpawner();
            LastSnapshot = null;
            ClearAllReplanRequests();
            _initialReplanRequestedForCurrentGame = false;
            ClearInProgressHeadFirePoint();
            ClearPendingDeadEndReport();
            _testCollectablesScriptedLifeLossHook?.Reset();
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

            EnsureInitialReplanRequested();

            // Сначала снимаем snapshot для текущего execution-тика.
            LastSnapshot = _snapshotBuilder.Build(_hamster);
            PlanExecutionTickResult executionResult = _executor.Tick(_hamster);

            // Переснимаем snapshot только после фактического execution-перехода.
            // В обычных кадрах ожидания исходный snapshot остаётся актуальным для решения о rebuild.
            if (executionResult != PlanExecutionTickResult.None)
                LastSnapshot = _snapshotBuilder.Build(_hamster);

            UpdateInProgressHeadFirePoint(executionResult);
            PromoteDeferredReplanReasons();
            RequestReplanForExecutionResult(executionResult);

            if (ShouldRebuildPlan())
                RebuildPlanFromCurrentSnapshot();
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
        /// Проверяет, нужно ли строить план заново в текущем tick.
        /// </summary>
        private bool ShouldRebuildPlan()
        {
            return _isReplanRequested;
        }

        /// <summary>
        /// Строит новый план от текущего snapshot с сохранением уже запущенного head-action.
        /// </summary>
        private void RebuildPlanFromCurrentSnapshot()
        {
            // Проверяет готовность planning компонентов.
            if (_executor == null || _planBuilder == null)
                return;

            BotReplanReason replanReasons = ConsumeReplanReasons();
            if (replanReasons == BotReplanReason.None)
                return;

            // Строит candidate plan с учетом текущего execution state.
            PlanBuildResult buildResult = BuildPlanForCurrentExecutionState(replanReasons);
            if (buildResult.HasDeadEnd)
                RememberPendingDeadEndReport(buildResult.DeadEndReport, replanReasons);
            else
                ClearPendingDeadEndReport();

            BotPlan plan = buildResult.Plan;

            // Отбрасывает только эквивалентный plan; пустой rebuild тоже должен очищать старый хвост.
            if (plan.IsEquivalentTo(CurrentPlan))
                return;

            // Активирует новый plan.
            _executor.SetPlan(plan);
            if (plan.HasActions)
                LogPlanActivation(plan, replanReasons);
        }

        /// <summary>
        /// Строит candidate plan: live-root при ожидании action или committed-head плюс новый хвост при execution.
        /// </summary>
        private PlanBuildResult BuildPlanForCurrentExecutionState(BotReplanReason replanReasons)
        {
            if (!CurrentPlan.HasActions
                || HasReplanReason(replanReasons, BotReplanReason.ActionCancelled))
            {
                return _planBuilder.Build(LastSnapshot);
            }

            IReadOnlyList<PlannedAction> committedPrefix = BuildCommittedPrefix(CurrentPlan);
            if (committedPrefix.Count == 0)
                return _planBuilder.Build(LastSnapshot);

            PlanningState rootState = PlanningState.FromSnapshot(LastSnapshot);
            PlanningState tailRootState = BuildTailRootState(rootState, committedPrefix);

            if (tailRootState == null)
                return _planBuilder.Build(LastSnapshot);

            PlanBuildResult tailBuildResult = _planBuilder.Build(LastSnapshot, tailRootState);
            BotPlan tailPlan = tailBuildResult.Plan;

            var actions = new List<PlannedAction>(committedPrefix.Count + tailPlan.Actions.Count);
            for (int actionIndex = 0; actionIndex < committedPrefix.Count; actionIndex++)
                actions.Add(committedPrefix[actionIndex]);

            for (int actionIndex = 0; actionIndex < tailPlan.Actions.Count; actionIndex++)
                actions.Add(tailPlan.Actions[actionIndex]);

            return new PlanBuildResult(
                new BotPlan(actions, tailPlan.CommittedBoundaryX, tailPlan.Score),
                tailBuildResult.DeadEndReport);
        }

        /// <summary>
        /// Сохраняет planning-diagnosis до фактической потери жизни.
        /// </summary>
        private void RememberPendingDeadEndReport(
            PlanningDeadEndReport deadEndReport,
            BotReplanReason replanReasons)
        {
            if (deadEndReport == null)
                return;

            _pendingDeadEndReport = deadEndReport;
            _pendingDeadEndReplanReasons = replanReasons;
        }

        /// <summary>
        /// Очищает pending diagnosis после успешного replan или сброса runtime state.
        /// </summary>
        private void ClearPendingDeadEndReport()
        {
            _pendingDeadEndReport = null;
            _pendingDeadEndReplanReasons = BotReplanReason.None;
        }

        /// <summary>
        /// Подтверждает dead-end только после фактической потери жизни.
        /// </summary>
        private void OnLivesLost(int livesLost)
        {
            if (_testCollectablesScriptedLifeLossHook?.TryConsumeLivesLost(livesLost) == true)
                return;

            if (!IsEnabled || _pendingDeadEndReport == null)
                return;

            ReportConfirmedDeadEnd(_pendingDeadEndReport, _pendingDeadEndReplanReasons, livesLost);
            ClearPendingDeadEndReport();
        }

        /// <summary>
        /// Логирует подтвержденный непроходимый участок уровня и останавливает validation run.
        /// </summary>
        private void ReportConfirmedDeadEnd(
            PlanningDeadEndReport deadEndReport,
            BotReplanReason replanReasons,
            int livesLost)
        {
            if (deadEndReport == null)
                return;

            string header =
                $"[Bot DEAD_END] confirmed=true reason={FormatReplanReasons(replanReasons)} " +
                $"livesLost={livesLost} lives={(_hamster != null ? _hamster.Lives.Value : -1)} " +
                $"depth={deadEndReport.Depth} " +
                $"nextObstacleIndex={deadEndReport.NextObstacleIndex} " +
                $"projection={deadEndReport.ProjectionWorldShift:F2}";
            string causes = FormatDeadEndReasons(deadEndReport);

            DebugManager.DiagLog(header);
            DebugManager.DiagLog("[Bot DEAD_END] causes:");
            LogDeadEndReasonLines(deadEndReport);
            Debug.LogWarning($"{header}{Environment.NewLine}causes:{Environment.NewLine}{causes}");
            DebugManager.DiagLog("[TEST RESULT] FAIL");
            DebugManager.DiagStability("[TEST RESULT] FAIL");
            _gameManager?.Pause();
        }

        /// <summary>
        /// Форматирует причины dead-end от применимых стратегий.
        /// </summary>
        private static string FormatDeadEndReasons(PlanningDeadEndReport deadEndReport)
        {
            if (deadEndReport?.Reasons == null || deadEndReport.Reasons.Count == 0)
                return "Применимые стратегии не вернули действия, но dead-end причины не собраны.";

            var builder = new StringBuilder();
            for (int reasonIndex = 0; reasonIndex < deadEndReport.Reasons.Count; reasonIndex++)
            {
                if (reasonIndex > 0)
                    builder.AppendLine();

                builder.Append(deadEndReport.Reasons[reasonIndex]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Пишет причины dead-end отдельными diagnostic-строками.
        /// </summary>
        private static void LogDeadEndReasonLines(PlanningDeadEndReport deadEndReport)
        {
            if (deadEndReport?.Reasons == null || deadEndReport.Reasons.Count == 0)
            {
                DebugManager.DiagLog("[Bot DEAD_END] Применимые стратегии не вернули действия, но dead-end причины не собраны.");
                return;
            }

            for (int reasonIndex = 0; reasonIndex < deadEndReport.Reasons.Count; reasonIndex++)
                DebugManager.DiagLog($"[Bot DEAD_END] {deadEndReport.Reasons[reasonIndex]}");
        }

        /// <summary>
        /// Возвращает committed-prefix, который должен пережить replan без замены.
        /// </summary>
        private static IReadOnlyList<PlannedAction> BuildCommittedPrefix(BotPlan plan)
        {
            if (plan == null || !plan.HasActions)
                return Array.Empty<PlannedAction>();

            int committedActionCount = Math.Min(_committedPrefixActionCount, plan.Actions.Count);
            var committedActions = new PlannedAction[committedActionCount];
            for (int actionIndex = 0; actionIndex < committedActionCount; actionIndex++)
                committedActions[actionIndex] = plan.Actions[actionIndex];

            return committedActions;
        }

        /// <summary>
        /// Получает root-состояние для хвоста после committed-prefix.
        /// </summary>
        private PlanningState BuildTailRootState(
            PlanningState rootState,
            IReadOnlyList<PlannedAction> committedPrefix)
        {
            PlanningState currentState = rootState;
            for (int actionIndex = 0; actionIndex < committedPrefix.Count; actionIndex++)
            {
                PlannedAction committedAction = committedPrefix[actionIndex];
                bool isCurrentInProgressHead = actionIndex == 0 && _executor.IsActionInProgress;

                currentState = isCurrentInProgressHead
                    ? ProjectInProgressCommittedAction(currentState, committedAction)
                    : SimulatePendingCommittedAction(currentState, committedAction);

                if (currentState == null)
                    return null;
            }

            return currentState;
        }

        /// <summary>
        /// Проецирует уже запущенный committed action до его ожидаемого завершения.
        /// </summary>
        private PlanningState ProjectInProgressCommittedAction(
            PlanningState currentState,
            PlannedAction committedAction)
        {
            float? remainingPostFireWorldShift = TryGetRemainingPostFireWorldShift(
                committedAction,
                out float remainingShift)
                    ? remainingShift
                    : null;

            return _transitionSimulator.ProjectInProgress(
                currentState,
                committedAction,
                LastSnapshot,
                remainingPostFireWorldShift);
        }

        /// <summary>
        /// Симулирует pending committed action для построения хвоста после него.
        /// </summary>
        private PlanningState SimulatePendingCommittedAction(
            PlanningState currentState,
            PlannedAction committedAction)
        {
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(LastSnapshot, currentState);
            if (projectedWorldSnapshot == null)
                return null;

            PlannedAction projectionAction = CreatePendingProjectionAction(
                committedAction,
                projectedWorldSnapshot);

            return _transitionSimulator.Simulate(
                currentState,
                projectionAction,
                LastSnapshot);
        }

        /// <summary>
        /// Обновляет fire point текущего in-progress head-action.
        /// </summary>
        private void UpdateInProgressHeadFirePoint(PlanExecutionTickResult executionResult)
        {
            if (HasExecutionResult(executionResult, PlanExecutionTickResult.Completed)
                || HasExecutionResult(executionResult, PlanExecutionTickResult.Cancelled))
            {
                ClearInProgressHeadFirePoint();
            }

            if (HasExecutionResult(executionResult, PlanExecutionTickResult.Fired) && CurrentPlan.HasActions)
            {
                _inProgressHeadAction = CurrentPlan.Actions[0];
                _inProgressHeadFireTime = LastSnapshot.SnapshotTime;
            }
        }

        /// <summary>
        /// Запрашивает пересборку плана по результатам execution tick.
        /// </summary>
        private void RequestReplanForExecutionResult(PlanExecutionTickResult executionResult)
        {
            if (HasExecutionResult(executionResult, PlanExecutionTickResult.Completed))
                RequestDeferredReplan(BotReplanReason.ActionCompleted);

            if (HasExecutionResult(executionResult, PlanExecutionTickResult.Cancelled))
                RequestReplan(BotReplanReason.ActionCancelled);
        }

        /// <summary>
        /// Проверяет наличие указанного execution-факта в результате tick.
        /// </summary>
        private static bool HasExecutionResult(
            PlanExecutionTickResult executionResult,
            PlanExecutionTickResult expectedResult)
        {
            return (executionResult & expectedResult) != PlanExecutionTickResult.None;
        }

        /// <summary>
        /// Проверяет наличие указанной причины в наборе причин replan.
        /// </summary>
        private static bool HasReplanReason(
            BotReplanReason replanReasons,
            BotReplanReason expectedReason)
        {
            return (replanReasons & expectedReason) != BotReplanReason.None;
        }

        /// <summary>
        /// Запрашивает первичную пересборку плана для текущего gameplay runtime.
        /// </summary>
        private void RequestInitialReplan(BotReplanReason reason)
        {
            _initialReplanRequestedForCurrentGame = true;
            RequestReplan(reason);
        }

        /// <summary>
        /// Страхует случай, когда controller зарегистрировался после вызова GameManager.StartGame().
        /// </summary>
        private void EnsureInitialReplanRequested()
        {
            if (_initialReplanRequestedForCurrentGame)
                return;

            RequestInitialReplan(BotReplanReason.LevelStart);
        }

        /// <summary>
        /// Добавляет причину event-driven пересборки плана.
        /// </summary>
        private void RequestReplan(BotReplanReason reason)
        {
            if (reason == BotReplanReason.None)
                return;

            _pendingReplanReasons |= reason;
            _isReplanRequested = true;
        }

        /// <summary>
        /// Откладывает пересборку плана до следующего bot tick, чтобы completion-кадр не делал ещё и planning.
        /// </summary>
        private void RequestDeferredReplan(BotReplanReason reason)
        {
            if (reason == BotReplanReason.None)
                return;

            _deferredReplanReasons |= reason;
        }

        /// <summary>
        /// Переносит отложенные причины в обычный request state в начале следующего bot tick.
        /// </summary>
        private void PromoteDeferredReplanReasons()
        {
            if (_deferredReplanReasons == BotReplanReason.None)
                return;

            _pendingReplanReasons |= _deferredReplanReasons;
            _isReplanRequested = true;
            _deferredReplanReasons = BotReplanReason.None;
        }

        /// <summary>
        /// Возвращает накопленные причины пересборки и очищает request state.
        /// </summary>
        private BotReplanReason ConsumeReplanReasons()
        {
            BotReplanReason reasons = _pendingReplanReasons;
            ClearPendingReplanRequest();
            return reasons;
        }

        /// <summary>
        /// Очищает immediate request state пересборки плана.
        /// </summary>
        private void ClearPendingReplanRequest()
        {
            _isReplanRequested = false;
            _pendingReplanReasons = BotReplanReason.None;
        }

        /// <summary>
        /// Очищает весь request state пересборки плана при сбросе runtime.
        /// </summary>
        private void ClearAllReplanRequests()
        {
            ClearPendingReplanRequest();
            _deferredReplanReasons = BotReplanReason.None;
        }

        /// <summary>
        /// Возвращает оставшийся world shift для уже запущенной head-action.
        /// </summary>
        private bool TryGetRemainingPostFireWorldShift(PlannedAction action, out float remainingPostFireWorldShift)
        {
            remainingPostFireWorldShift = 0f;
            if (_inProgressHeadAction == null
                || action == null
                || !action.IsEquivalentTo(_inProgressHeadAction)
                || LastSnapshot == null)
            {
                return false;
            }

            float elapsedSeconds = LastSnapshot.SnapshotTime - _inProgressHeadFireTime;
            float elapsedWorldShift = elapsedSeconds > 0f
                ? elapsedSeconds * Consts.GameSpeedBase
                : 0f;
            remainingPostFireWorldShift = action.PostFireWorldShift - elapsedWorldShift;
            if (remainingPostFireWorldShift < 0f)
                remainingPostFireWorldShift = 0f;

            return true;
        }

        /// <summary>
        /// Сбрасывает fire point in-progress action.
        /// </summary>
        private void ClearInProgressHeadFirePoint()
        {
            _inProgressHeadAction = null;
            _inProgressHeadFireTime = 0f;
        }

        /// <summary>
        /// Создает projection action для waiting head с remaining completion-shift от live trigger.
        /// </summary>
        private static PlannedAction CreatePendingProjectionAction(
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            float remainingCompletionWorldShift = GetRemainingCompletionWorldShift(
                action,
                worldSnapshot);

            return CopyActionWithWorldShifts(
                action,
                remainingCompletionWorldShift,
                action.PostFireWorldShift);
        }

        /// <summary>
        /// Считает оставшийся world shift до завершения waiting action.
        /// </summary>
        private static float GetRemainingCompletionWorldShift(
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (!triggerObstacleInstanceId.HasValue
                || !TryFindTriggerObstacle(triggerObstacleInstanceId.Value, worldSnapshot, out ObstacleSnapshot triggerObstacle))
            {
                return action.CompletionWorldShift;
            }

            float remainingPreFireWorldShift = triggerObstacle.LeftX > action.TriggerX
                ? triggerObstacle.LeftX - action.TriggerX
                : 0f;

            return remainingPreFireWorldShift + action.PostFireWorldShift;
        }

        /// <summary>
        /// Копирует planning action с заменой world-shift полей для projection.
        /// </summary>
        private static PlannedAction CopyActionWithWorldShifts(
            PlannedAction action,
            float completionWorldShift,
            float postFireWorldShift)
        {
            return new PlannedAction(
                action.Kind,
                action.TriggerX,
                action.RenderWorldX,
                completionWorldShift,
                postFireWorldShift,
                action.TargetObstacleIndex,
                action.TargetObstacleInstanceId,
                action.TriggerObstacleInstanceId,
                action.TargetBottomLine,
                action.EnergyCost,
                action.Description,
                action.ResultRoofSupportInstanceId,
                action.FulfillsJumpOnObjective,
                action.IsOppositeLaneEntry,
                action.TriggerWindow,
                action.CollectibleObjectiveValue);
        }

        /// <summary>
        /// Ищет trigger obstacle в текущем snapshot.
        /// </summary>
        private static bool TryFindTriggerObstacle(
            int triggerObstacleInstanceId,
            WorldSnapshot worldSnapshot,
            out ObstacleSnapshot triggerObstacle)
        {
            triggerObstacle = null;
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.InstanceId != triggerObstacleInstanceId)
                    continue;

                triggerObstacle = obstacle;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Пишет краткую диагностическую строку для только что активированного плана.
        /// </summary>
        private static void LogPlanActivation(BotPlan plan, BotReplanReason replanReasons)
        {
            string message = $"[Bot PLAN] {FormatPlanChain(plan)}";

            DebugManager.DiagLog(message);
            Debug.Log(message);
        }

        /// <summary>
        /// Форматирует набор причин пересборки плана для диагностической строки.
        /// </summary>
        private static string FormatReplanReasons(BotReplanReason replanReasons)
        {
            return replanReasons.ToString().Replace(", ", "|");
        }

        /// <summary>
        /// Форматирует последовательность действий плана в компактную chain-строку.
        /// </summary>
        private static string FormatPlanChain(BotPlan plan)
        {
            var builder = new StringBuilder();
            for (int actionIndex = 0; actionIndex < plan.Actions.Count; actionIndex++)
            {
                if (actionIndex > 0)
                    builder.Append(" -> ");

                builder.Append(FormatPlanAction(plan.Actions[actionIndex]));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Форматирует одно действие плана для диагностической chain-строки.
        /// </summary>
        private static string FormatPlanAction(PlannedAction action)
        {
            if (action.FulfillsCollectibleObjective)
                return $"{action.Kind}[{action.CollectibleObjectiveValue.Kind}:{action.CollectibleObjectiveValue.EffectiveGain}]";

            return action.Kind.ToString();
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
            SubscribeToObstacleSpawner(RuntimeObstacleSpawner.Instance);

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

            bool isChangingGameManager = !ReferenceEquals(_registeredGameManager, null);

            // Перерегистрирует controller в актуальный game manager.
            UnregisterFromGameManager();
            if (isChangingGameManager)
                ResetRuntimeStateForNewGameManager();

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

        /// <summary>
        /// Очищает runtime state, привязанный к предыдущему GameManager.
        /// </summary>
        private void ResetRuntimeStateForNewGameManager()
        {
            LastSnapshot = null;
            UnsubscribeFromObstacleSpawner();
            _eventTracker?.Dispose();
            _eventTracker = null;
            ClearAllReplanRequests();
            _initialReplanRequestedForCurrentGame = false;
            ClearInProgressHeadFirePoint();
            ClearPendingDeadEndReport();
            _testCollectablesScriptedLifeLossHook?.Reset();
            _executor?.Clear();
        }

        /// <summary>
        /// Подписывает controller на события текущего obstacle spawner.
        /// </summary>
        private void SubscribeToObstacleSpawner(RuntimeObstacleSpawner obstacleSpawner)
        {
            if (ReferenceEquals(_subscribedObstacleSpawner, obstacleSpawner))
                return;

            UnsubscribeFromObstacleSpawner();
            if (obstacleSpawner == null)
                return;

            _subscribedObstacleSpawner = obstacleSpawner;
            _subscribedObstacleSpawner.PatternSpawned += OnPatternSpawned;
            ApplySpawnLookaheadToObstacleSpawner();
        }

        /// <summary>
        /// Отписывает controller от событий текущего obstacle spawner.
        /// </summary>
        private void UnsubscribeFromObstacleSpawner()
        {
            if (!ReferenceEquals(_subscribedObstacleSpawner, null))
            {
                _subscribedObstacleSpawner.SpawnLookaheadPatterns = _defaultSpawnLookaheadPatterns;
                _subscribedObstacleSpawner.PatternSpawned -= OnPatternSpawned;
            }

            _subscribedObstacleSpawner = null;
        }

        /// <summary>
        /// Синхронизирует окно спауна с текущим состоянием runtime-бота.
        /// </summary>
        private void ApplySpawnLookaheadToObstacleSpawner()
        {
            if (ReferenceEquals(_subscribedObstacleSpawner, null))
                return;

            _subscribedObstacleSpawner.SpawnLookaheadPatterns =
                IsEnabled ? _botSpawnLookaheadPatterns : _defaultSpawnLookaheadPatterns;
        }

        /// <summary>
        /// Запрашивает пересборку хвоста после появления нового pattern.
        /// </summary>
        private void OnPatternSpawned(int patternIndex, string patternName)
        {
            if (!IsEnabled)
                return;

            _testCollectablesScriptedLifeLossHook?.TryApplyBeforePatternEvaluation(patternIndex, patternName);
            RequestReplan(BotReplanReason.SpawnPattern);
            DebugManager.DiagLog(
                $"[Bot PATTERN] SPAWN patternIndex={patternIndex} pattern={patternName}");
        }
    }
}
