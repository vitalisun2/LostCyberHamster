using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
using Assets.Scripts.Bot.Strategies.RoofSwitchLane;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOver;
using Assets.Scripts.Bot.Strategies.SuperJumpOn;
using Assets.Scripts.Bot.Strategies.SuperJumpOnFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOnRoof;
using Assets.Scripts.Bot.Strategies.SuperRoofJumpOver;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.GameEngine.Mechanics;
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
        /// Текущий async replan task, если сборка уже выполняется в worker.
        /// </summary>
        private Task<AsyncPlanBuildResult> _runningReplanTask;

        /// <summary>
        /// Id текущего async request-а для отбрасывания устаревших результатов.
        /// </summary>
        private int _runningReplanRequestId;

        /// <summary>
        /// Следующий id async request-а.
        /// </summary>
        private int _nextReplanRequestId;

        /// <summary>
        /// Поколение runtime state; меняется при сбросе scene/controller state.
        /// </summary>
        private int _runtimeGeneration;

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
            DebugManager.SetVerboseDiagLoggingEnabled(false);
            BotDiagnostics.Reset();
            BotAnimationTravelProvider.PrewarmKnownClipData();
            ApplyObstacleBonusDropPolicy();

            IReadOnlyList<IPlanningStrategy> executorStrategies = CreatePlanningStrategies();
            _executor = new PlanExecutor(executorStrategies);
            _testCollectablesScriptedLifeLossHook = new TestCollectablesScriptedLifeLossHook(
                () => _hamster,
                ClearPendingDeadEndReport);

            GameEventsManager.OnLivesLost += OnLivesLost;
        }

        /// <summary>
        /// Создает независимый набор planning strategies для executor или worker planner.
        /// </summary>
        private static IReadOnlyList<IPlanningStrategy> CreatePlanningStrategies()
        {
            var strategies = new IPlanningStrategy[]
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
                new RoofSwitchLaneStrategy(),
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

            AssertSuperFallbackStrategyOrder(strategies);
            return strategies;
        }

        /// <summary>
        /// Диагностирует нарушение контракта генерации fallback-actions:
        /// ordinary strategy должна идти раньше matching super strategy.
        /// </summary>
        private static void AssertSuperFallbackStrategyOrder(IReadOnlyList<IPlanningStrategy> strategies)
        {
            if (SuperFallbackActionDeduplicator.IsStrategyOrderValid(strategies))
                return;

            string message = "[Bot STRATEGY_ORDER_ASSERT] Super fallback strategy order is invalid. " +
                "Ordinary fallback strategies must be registered before matching super strategies. " +
                SuperFallbackActionDeduplicator.BuildStrategyOrderDiagnostic(strategies);

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Essential,
                message);
            Debug.Assert(false, message);
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
            InvalidateAsyncReplan();
            UnsubscribeFromObstacleSpawner();
            UnregisterFromGameManager();
            GameEventsManager.OnLivesLost -= OnLivesLost;
            _eventTracker?.Dispose();
            ObstacleBonusDropPolicyProvider.UseDefault();
        }

        /// <summary>
        /// Включает bot controller и пытается восстановить runtime-зависимости.
        /// </summary>
        private void Enable()
        {
            // Переводит controller в активное состояние.
            IsEnabled = true;
            ApplyObstacleBonusDropPolicy();
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
            ApplyObstacleBonusDropPolicy();
            InvalidateAsyncReplan();
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
        /// Настраивает drop policy для runtime-наград от уничтоженных препятствий.
        /// </summary>
        private void ApplyObstacleBonusDropPolicy()
        {
            if (IsEnabled)
            {
                ObstacleBonusDropPolicyProvider.UseNoEnergyBonuses();
                return;
            }

            ObstacleBonusDropPolicyProvider.UseDefault();
        }

        /// <summary>
        /// Выполняет шаг бота: обновляет восприятие, продвигает текущий план и при необходимости активирует новый.
        /// </summary>
        private void TickBot()
        {
            // Проверяет готовность planning компонентов.
            if (_executor == null)
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

            TryApplyCompletedAsyncReplan();

            if (ShouldRebuildPlan())
                StartAsyncReplanFromCurrentSnapshot();
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
        /// Запускает async пересборку плана от текущего snapshot без блокировки game tick.
        /// </summary>
        private void StartAsyncReplanFromCurrentSnapshot()
        {
            if (LastSnapshot == null)
                return;

            BotReplanReason replanReasons = ConsumeReplanReasons();
            if (replanReasons == BotReplanReason.None)
                return;

            AsyncPlanBuildRequest request = CaptureAsyncPlanBuildRequest(replanReasons);
            _runningReplanRequestId = request.RequestId;
            _runningReplanTask = Task.Run(() => new AsyncPlanRebuilder(CreatePlanningStrategies()).Build(request));
        }

        /// <summary>
        /// Применяет завершенный async replan result, если runtime state не успел устареть.
        /// </summary>
        private void TryApplyCompletedAsyncReplan()
        {
            Task<AsyncPlanBuildResult> task = _runningReplanTask;
            if (task == null || !task.IsCompleted)
                return;

            int requestId = _runningReplanRequestId;
            _runningReplanTask = null;
            _runningReplanRequestId = 0;

            if (task.IsCanceled)
                return;

            if (task.IsFaulted)
            {
                Debug.LogError($"[Bot] Async replan task failed: {task.Exception}");
                return;
            }

            AsyncPlanBuildResult result = task.Result;
            if (result == null
                || result.RequestId != requestId
                || result.RuntimeGeneration != _runtimeGeneration)
            {
                return;
            }

            if (HasQueuedReplanReasons())
            {
                return;
            }

            if (result.HasError)
            {
                Debug.LogError($"[Bot] Async replan failed: {result.Error}");
                RequestReplan(result.ReplanReasons);
                return;
            }

            if (!IsAsyncResultApplicableToCurrentExecution(result))
            {
                RequestReplan(result.ReplanReasons);
                return;
            }

            if (ShouldPreserveCurrentHandoffTail(result))
                return;

            LogAsyncHeadWindowDiagnostics(result);
            ApplyPlanBuildResult(result.BuildResult, result.ReplanReasons);
        }

        /// <summary>
        /// Временная диагностика: async-result может устареть до первого action window.
        /// </summary>
        private void LogAsyncHeadWindowDiagnostics(AsyncPlanBuildResult result)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Replan, BotDiagnosticLevel.Verbose))
                return;

            BotPlan plan = result?.BuildResult?.Plan;
            if (plan == null || !plan.HasActions || LastSnapshot == null)
                return;

            PlannedAction head = plan.Actions[0];
            if (head == null || !head.TriggerWindow.HasValue || !head.TriggerWindow.Value.IsValid)
                return;

            int? triggerObstacleInstanceId = head.TriggerObstacleInstanceId ?? head.TargetObstacleInstanceId;
            if (!triggerObstacleInstanceId.HasValue
                || !TryFindTriggerObstacle(triggerObstacleInstanceId.Value, LastSnapshot, out ObstacleSnapshot triggerObstacle))
            {
                return;
            }

            ActionTriggerWindow triggerWindow = head.TriggerWindow.Value;
            float liveObstacleLeftX = triggerObstacle.LeftX;
            bool afterWindowClose = liveObstacleLeftX < triggerWindow.LatestTriggerX - 0.001f;
            if (!afterWindowClose)
                return;

            float snapshotAgeSeconds = result.SnapshotTime > 0f
                ? LastSnapshot.SnapshotTime - result.SnapshotTime
                : 0f;
            float snapshotAgeWorldShift = snapshotAgeSeconds > 0f
                ? snapshotAgeSeconds * Consts.GameSpeedBase
                : 0f;

            BotReplanDiagnostics.LogAsyncHeadWindow(
                FormatReplanReasons(result.ReplanReasons),
                snapshotAgeSeconds,
                snapshotAgeWorldShift,
                head,
                triggerObstacleInstanceId,
                liveObstacleLeftX,
                triggerWindow.LatestTriggerX - liveObstacleLeftX,
                FormatPlanChain(plan));
        }

        /// <summary>
        /// Не применяет dead-end fallback, если он стирает следующий action после committed head.
        /// </summary>
        private bool ShouldPreserveCurrentHandoffTail(AsyncPlanBuildResult result)
        {
            if (_executor == null || !_executor.IsHeadCommitted)
                return false;

            PlanBuildResult buildResult = result?.BuildResult;
            if (buildResult == null || !buildResult.HasDeadEnd)
                return false;

            BotPlan currentPlan = CurrentPlan;
            BotPlan resultPlan = buildResult.Plan;
            return currentPlan.HasActions
                && currentPlan.Actions.Count > 1
                && resultPlan != null
                && resultPlan.HasActions
                && resultPlan.Actions.Count < 2
                && currentPlan.Actions[0].IsEquivalentTo(resultPlan.Actions[0]);
        }

        /// <summary>
        /// Захватывает immutable input для worker-пересборки.
        /// </summary>
        private AsyncPlanBuildRequest CaptureAsyncPlanBuildRequest(BotReplanReason replanReasons)
        {
            return new AsyncPlanBuildRequest(
                ++_nextReplanRequestId,
                _runtimeGeneration,
                LastSnapshot,
                CopyCurrentPlanForAsyncRequest(),
                _executor.IsActionInProgress,
                _executor.IsHeadCommitted,
                _inProgressHeadAction,
                _inProgressHeadFireTime,
                replanReasons);
        }

        /// <summary>
        /// Копирует action-list текущего плана, чтобы worker не зависел от дальнейшей замены CurrentPlan.
        /// </summary>
        private BotPlan CopyCurrentPlanForAsyncRequest()
        {
            BotPlan currentPlan = CurrentPlan;
            if (currentPlan == null || !currentPlan.HasActions)
                return BotPlan.Empty(currentPlan?.CommittedBoundaryX ?? 0f);

            var actions = new PlannedAction[currentPlan.Actions.Count];
            for (int actionIndex = 0; actionIndex < currentPlan.Actions.Count; actionIndex++)
                actions[actionIndex] = currentPlan.Actions[actionIndex];

            return new BotPlan(actions, currentPlan.CommittedBoundaryX, currentPlan.Score);
        }

        /// <summary>
        /// Применяет результат построения плана по старому main-thread контракту.
        /// </summary>
        private void ApplyPlanBuildResult(PlanBuildResult buildResult, BotReplanReason replanReasons)
        {
            if (buildResult == null)
                return;

            if (buildResult.HasDeadEnd)
                RememberPendingDeadEndReport(buildResult.DeadEndReport, replanReasons);
            else
                ClearPendingDeadEndReport();

            BotPlan plan = buildResult.Plan;
            LogPlanBuildResultDiagnostics(buildResult, replanReasons);
            if (plan.IsEquivalentTo(CurrentPlan))
                return;

            _executor.SetPlan(plan);
            if (plan.HasActions)
                LogPlanActivation(plan, replanReasons);
        }

        /// <summary>
        /// Проверяет, выполняется ли replan task в worker.
        /// </summary>
        private bool IsAsyncReplanRunning()
        {
            return _runningReplanTask != null && !_runningReplanTask.IsCompleted;
        }

        /// <summary>
        /// Проверяет, появились ли более свежие причины replan-а после запуска worker task.
        /// </summary>
        private bool HasQueuedReplanReasons()
        {
            return _pendingReplanReasons != BotReplanReason.None
                || _deferredReplanReasons != BotReplanReason.None;
        }

        /// <summary>
        /// Инвалидирует pending async result при смене runtime context.
        /// </summary>
        private void InvalidateAsyncReplan()
        {
            _runtimeGeneration++;
            _runningReplanTask = null;
            _runningReplanRequestId = 0;
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

            BotReplanDiagnostics.LogDeadEndHeader(
                FormatReplanReasons(replanReasons),
                $"livesLost={livesLost} lives={(_hamster != null ? _hamster.Lives.Value : -1)} " +
                $"depth={deadEndReport.Depth} " +
                $"nextObstacleIndex={deadEndReport.NextObstacleIndex} " +
                $"projection={deadEndReport.ProjectionWorldShift:F2}");
            LogDeadEndReasonLines(deadEndReport);
            Debug.LogWarning($"{header}{Environment.NewLine}causes:{Environment.NewLine}{causes}");
            BotRuntimeEventDiagnostics.LogLevelFailed();
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
                BotReplanDiagnostics.LogDeadEndWithoutReasons();
                return;
            }

            for (int reasonIndex = 0; reasonIndex < deadEndReport.Reasons.Count; reasonIndex++)
                BotReplanDiagnostics.LogDeadEndCause(deadEndReport.Reasons[reasonIndex].ToString());
        }

        /// <summary>
        /// Проверяет, что async-result не пытается заменить execution-head, который стал committed после capture.
        /// </summary>
        private bool IsAsyncResultApplicableToCurrentExecution(AsyncPlanBuildResult result)
        {
            if (result == null)
                return false;

            if (_executor == null || !_executor.IsHeadCommitted)
                return true;

            if (!result.WasHeadCommittedAtCapture)
                return false;

            BotPlan resultPlan = result.BuildResult?.Plan;
            return CurrentPlan.HasActions
                && resultPlan != null
                && resultPlan.HasActions
                && CurrentPlan.Actions[0].IsEquivalentTo(resultPlan.Actions[0]);
        }

        /// <summary>
        /// Возвращает execution-head, который уже нельзя заменить свежим replan-ом.
        /// </summary>
        private static IReadOnlyList<PlannedAction> BuildCommittedPrefix(AsyncPlanBuildRequest request)
        {
            if (request?.CurrentPlan == null
                || !request.CurrentPlan.HasActions
                || !request.IsHeadCommitted)
            {
                return Array.Empty<PlannedAction>();
            }

            // Fired и Waiting head уже принадлежат execution-слою: replan может перестроить только хвост.
            return new[] { request.CurrentPlan.Actions[0] };
        }

        /// <summary>
        /// Immutable input для async replan worker-а.
        /// </summary>
        private sealed class AsyncPlanBuildRequest
        {
            public AsyncPlanBuildRequest(
                int requestId,
                int runtimeGeneration,
                WorldSnapshot snapshot,
                BotPlan currentPlan,
                bool isActionInProgress,
                bool isHeadCommitted,
                PlannedAction inProgressHeadAction,
                float inProgressHeadFireTime,
                BotReplanReason replanReasons)
            {
                RequestId = requestId;
                RuntimeGeneration = runtimeGeneration;
                Snapshot = snapshot;
                CurrentPlan = currentPlan ?? BotPlan.Empty(snapshot?.ScreenRightEdgeX ?? 0f);
                IsActionInProgress = isActionInProgress;
                IsHeadCommitted = isHeadCommitted;
                InProgressHeadAction = inProgressHeadAction;
                InProgressHeadFireTime = inProgressHeadFireTime;
                ReplanReasons = replanReasons;
            }

            public int RequestId { get; }
            public int RuntimeGeneration { get; }
            public WorldSnapshot Snapshot { get; }
            public BotPlan CurrentPlan { get; }
            public bool IsActionInProgress { get; }
            public bool IsHeadCommitted { get; }
            public PlannedAction InProgressHeadAction { get; }
            public float InProgressHeadFireTime { get; }
            public BotReplanReason ReplanReasons { get; }
        }

        /// <summary>
        /// Result async replan worker-а, включая exception без падения Task.
        /// </summary>
        private sealed class AsyncPlanBuildResult
        {
            private AsyncPlanBuildResult(
                int requestId,
                int runtimeGeneration,
                BotReplanReason replanReasons,
                float snapshotTime,
                bool wasHeadCommittedAtCapture,
                PlanBuildResult buildResult,
                Exception error)
            {
                RequestId = requestId;
                RuntimeGeneration = runtimeGeneration;
                ReplanReasons = replanReasons;
                SnapshotTime = snapshotTime;
                WasHeadCommittedAtCapture = wasHeadCommittedAtCapture;
                BuildResult = buildResult;
                Error = error;
            }

            public int RequestId { get; }
            public int RuntimeGeneration { get; }
            public BotReplanReason ReplanReasons { get; }
            public float SnapshotTime { get; }
            public bool WasHeadCommittedAtCapture { get; }
            public PlanBuildResult BuildResult { get; }
            public Exception Error { get; }
            public bool HasError => Error != null;

            public static AsyncPlanBuildResult Success(
                AsyncPlanBuildRequest request,
                PlanBuildResult buildResult)
            {
                return new AsyncPlanBuildResult(
                    request.RequestId,
                    request.RuntimeGeneration,
                    request.ReplanReasons,
                    request.Snapshot?.SnapshotTime ?? 0f,
                    request.IsHeadCommitted,
                    buildResult,
                    error: null);
            }

            public static AsyncPlanBuildResult Failure(
                AsyncPlanBuildRequest request,
                Exception error)
            {
                return new AsyncPlanBuildResult(
                    request?.RequestId ?? 0,
                    request?.RuntimeGeneration ?? 0,
                    request?.ReplanReasons ?? BotReplanReason.None,
                    request?.Snapshot?.SnapshotTime ?? 0f,
                    request?.IsHeadCommitted ?? false,
                    buildResult: null,
                    error);
            }
        }

        /// <summary>
        /// Собирает план в worker по захваченному snapshot и execution-state.
        /// </summary>
        private sealed class AsyncPlanRebuilder
        {
            private readonly TransitionSimulator _transitionSimulator;
            private readonly PlanBuilder _planBuilder;

            public AsyncPlanRebuilder(IReadOnlyList<IPlanningStrategy> strategies)
            {
                _transitionSimulator = new TransitionSimulator(strategies);
                _planBuilder = new PlanBuilder(
                    new ActionGenerator(strategies),
                    _transitionSimulator,
                    new PlanEvaluator());
            }

            public AsyncPlanBuildResult Build(AsyncPlanBuildRequest request)
            {
                try
                {
                    PlanBuildResult buildResult = BuildPlanForRequest(request);
                    return AsyncPlanBuildResult.Success(
                        request,
                        buildResult);
                }
                catch (Exception error)
                {
                    return AsyncPlanBuildResult.Failure(request, error);
                }
            }

            private PlanBuildResult BuildPlanForRequest(AsyncPlanBuildRequest request)
            {
                if (request?.Snapshot == null)
                    return new PlanBuildResult(BotPlan.Empty(), deadEndReport: null);

                WorldSnapshot snapshot = request.Snapshot;
                if (!request.CurrentPlan.HasActions
                    || HasReplanReason(request.ReplanReasons, BotReplanReason.ActionCancelled))
                {
                    return _planBuilder.Build(snapshot);
                }

                IReadOnlyList<PlannedAction> committedPrefix = BuildCommittedPrefix(request);
                if (committedPrefix.Count == 0)
                    return _planBuilder.Build(snapshot);

                PlanningState rootState = PlanningState.FromSnapshot(snapshot);
                PlanningState tailRootState = BuildTailRootState(request, rootState, committedPrefix);

                if (tailRootState == null)
                    return _planBuilder.Build(snapshot);

                PlanBuildResult tailBuildResult = _planBuilder.Build(snapshot, tailRootState);
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

            private PlanningState BuildTailRootState(
                AsyncPlanBuildRequest request,
                PlanningState rootState,
                IReadOnlyList<PlannedAction> committedPrefix)
            {
                PlanningState currentState = rootState;
                for (int actionIndex = 0; actionIndex < committedPrefix.Count; actionIndex++)
                {
                    PlannedAction committedAction = committedPrefix[actionIndex];
                    bool isCurrentInProgressHead = actionIndex == 0 && request.IsActionInProgress;

                    currentState = isCurrentInProgressHead
                        ? ProjectInProgressCommittedAction(request, currentState, committedAction)
                        : SimulatePendingCommittedAction(request, currentState, committedAction);

                    if (currentState == null)
                        return null;
                }

                return currentState;
            }

            private PlanningState ProjectInProgressCommittedAction(
                AsyncPlanBuildRequest request,
                PlanningState currentState,
                PlannedAction committedAction)
            {
                float? remainingPostFireWorldShift = TryGetRemainingPostFireWorldShift(
                    request,
                    committedAction,
                    out float remainingShift)
                        ? remainingShift
                        : null;

                return _transitionSimulator.ProjectInProgress(
                    currentState,
                    committedAction,
                    request.Snapshot,
                    remainingPostFireWorldShift);
            }

            private PlanningState SimulatePendingCommittedAction(
                AsyncPlanBuildRequest request,
                PlanningState currentState,
                PlannedAction committedAction)
            {
                WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(request.Snapshot, currentState);
                if (projectedWorldSnapshot == null)
                    return null;

                PlannedAction projectionAction = CreatePendingProjectionAction(
                    committedAction,
                    projectedWorldSnapshot);

                return _transitionSimulator.Simulate(
                    currentState,
                    projectionAction,
                    request.Snapshot);
            }
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
        private static bool TryGetRemainingPostFireWorldShift(
            AsyncPlanBuildRequest request,
            PlannedAction action,
            out float remainingPostFireWorldShift)
        {
            remainingPostFireWorldShift = 0f;
            if (request?.InProgressHeadAction == null
                || action == null
                || !action.IsEquivalentTo(request.InProgressHeadAction)
                || request.Snapshot == null)
            {
                return false;
            }

            float elapsedSeconds = request.Snapshot.SnapshotTime - request.InProgressHeadFireTime;
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

            BotReplanDiagnostics.LogPlan(plan, FormatPlanChain(plan));
            Debug.Log(message);
        }

        /// <summary>
        /// Временная диагностика: связывает установленный plan с типом результата planning-графа.
        /// </summary>
        private static void LogPlanBuildResultDiagnostics(
            PlanBuildResult buildResult,
            BotReplanReason replanReasons)
        {
            BotPlan plan = buildResult?.Plan;
            if (plan == null || !plan.HasActions)
                return;

            if (!buildResult.HasDeadEnd
                && !HasActionKind(plan, BotActionKind.PassiveRoofExit)
                && !HasActionKind(plan, BotActionKind.SwitchLane)
                && !HasActionKind(plan, BotActionKind.RoofSwitchLane))
            {
                return;
            }

            PlanningDeadEndReport report = buildResult.DeadEndReport;
            BotReplanDiagnostics.LogPlanBuildResult(
                buildResult,
                FormatReplanReasons(replanReasons),
                FormatPlanChain(plan),
                FormatNullable(report?.Depth),
                FormatNullable(report?.NextObstacleIndex),
                FormatNullable(report?.ProjectionWorldShift));
        }

        private static bool HasActionKind(BotPlan plan, BotActionKind actionKind)
        {
            if (plan?.Actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < plan.Actions.Count; actionIndex++)
            {
                if (plan.Actions[actionIndex]?.Kind == actionKind)
                    return true;
            }

            return false;
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

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }

        private static string FormatNullable(float? value)
        {
            return value.HasValue ? value.Value.ToString("F2") : "none";
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

            BotAnimationTravelProvider.PrewarmKnownClipData();

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
            InvalidateAsyncReplan();
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
            BotReplanDiagnostics.LogPatternSpawn(
                patternIndex,
                patternName,
                FormatPatternObstacleIds(_subscribedObstacleSpawner, patternIndex));

            if (string.Equals(patternName, "roof_wide_gap", StringComparison.OrdinalIgnoreCase)
                || string.Equals(patternName, "shift_line_choice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(patternName, "shift_line_choice_2", StringComparison.OrdinalIgnoreCase))
            {
                BotReplanDiagnostics.LogPatternDetail(
                    patternIndex,
                    patternName,
                    FormatPatternObstacleDetails(_subscribedObstacleSpawner, patternIndex));
            }
        }

        private static string FormatPatternObstacleIds(
            RuntimeObstacleSpawner obstacleSpawner,
            int patternIndex)
        {
            if (obstacleSpawner?.SpawnedObstacles == null)
                return "none";

            var ids = new List<int>();
            for (int obstacleIndex = 0; obstacleIndex < obstacleSpawner.SpawnedObstacles.Count; obstacleIndex++)
            {
                var obstacle = obstacleSpawner.SpawnedObstacles[obstacleIndex];
                if (obstacle?.ObstacleScript == null || obstacle.PatternIndex != patternIndex)
                    continue;

                ids.Add(obstacle.ObstacleScript.GetInstanceID());
            }

            if (ids.Count == 0)
                return "none";

            ids.Sort();
            return string.Join(",", ids);
        }

        private static string FormatPatternObstacleDetails(
            RuntimeObstacleSpawner obstacleSpawner,
            int patternIndex)
        {
            if (obstacleSpawner?.SpawnedObstacles == null)
                return "none";

            var details = new List<string>();
            for (int obstacleIndex = 0; obstacleIndex < obstacleSpawner.SpawnedObstacles.Count; obstacleIndex++)
            {
                var instantiatedObstacle = obstacleSpawner.SpawnedObstacles[obstacleIndex];
                var obstacle = instantiatedObstacle?.ObstacleScript;
                if (obstacle == null || instantiatedObstacle.PatternIndex != patternIndex)
                    continue;

                Vector3 spawnPosition = instantiatedObstacle.SpawnPosition;
                Vector3 currentPosition = obstacle.transform.position;
                string lane = obstacle.ObstacleType.IsTop ? "top" : "bottom";
                details.Add(
                    $"{obstacle.GetInstanceID()}:{obstacle.ObstacleType.ObstacleTypeEnum}:{lane}:" +
                    $"spawn=({spawnPosition.x:F2},{spawnPosition.y:F2}):" +
                    $"pos=({currentPosition.x:F2},{currentPosition.y:F2}):" +
                    $"sprite={instantiatedObstacle.SpriteName}");
            }

            if (details.Count == 0)
                return "none";

            details.Sort(StringComparer.Ordinal);
            return string.Join("|", details);
        }
    }
}
