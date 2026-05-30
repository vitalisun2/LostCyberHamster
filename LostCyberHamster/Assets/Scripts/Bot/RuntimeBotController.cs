using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Execution;
using Assets.Scripts.Bot.Strategies.JumpFromRoof;
using Assets.Scripts.Bot.Strategies.JumpOnFromRoof;
using Assets.Scripts.Bot.Strategies.JumpFromRoofOnRoof;
using Assets.Scripts.Bot.Strategies.JumpOn;
using Assets.Scripts.Bot.Strategies.JumpOnRoof;
using Assets.Scripts.Bot.Strategies.JumpOver;
using Assets.Scripts.Bot.Strategies.RoofJumpOver;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.SuperRoofJumpOver;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOnFromRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOn;
using Assets.Scripts.Bot.Strategies.SuperJumpOnRoof;
using Assets.Scripts.Bot.Strategies.SuperJumpOver;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Common;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;
#if UNITY_EDITOR
using System;
using Unity.Profiling;
#endif

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Оркестрирует perception, planning и execution бота в рантайме.
    /// </summary>
    public sealed class RuntimeBotController : MonoBehaviour
    {
        private const float _initRetryInterval = 0.5f;
        private const string _hostObjectName = "[Bot]";
#if UNITY_EDITOR
        private const float PerfSummaryIntervalSeconds = 1f;
        private const float FrameSpikeThresholdMs = 30f;
        private const float BotTickSpikeThresholdMs = 15f;
        private const long GcAllocSpikeThresholdBytes = 1024 * 1024;
#endif

        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();

        private PlanExecutor _executor;
        private Hamster _hamster;
        private GameManager _gameManager;
        private PlanBuilder _planBuilder;
        private RuntimeBotEventTracker _eventTracker;
        private float _nextInitRetryTime;
#if UNITY_EDITOR
        private ProfilerRecorder _gcAllocatedInFrameRecorder;
        private ProfilerRecorder _gcUsedMemoryRecorder;
        private bool _perfDiagnosticsReady;
        private float _nextPerfSummaryTime;
        private int _perfFrameCount;
        private int _perfSpikeFrameCount;
        private long _perfTotalGcAllocBytes;
        private long _perfMaxGcAllocBytes;
        private float _perfMaxFrameMs;
        private float _perfMaxBotTickMs;
        private float _perfMaxSnapshotMs;
        private float _perfMaxExecutorMs;
        private float _perfMaxPlanBuildMs;
        private int _lastGen0CollectionCount;
        private int _lastGen1CollectionCount;
        private int _lastGen2CollectionCount;
        private float _lastSnapshotBuildMs;
        private float _lastExecutorTickMs;
        private float _lastPlanBuildMs;
#endif

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

            IReadOnlyList<IPlanningStrategy> strategies = CreateStrategies();

            _executor = new PlanExecutor(strategies);
            _planBuilder = new PlanBuilder(
                new ActionGenerator(strategies),
                new TransitionSimulator(strategies),
                new PlanEvaluator(),
                new RetainedActionRevalidator(strategies),
                new ActionInProgressProjector(strategies));

#if UNITY_EDITOR
            InitializePerfDiagnostics();
#endif
        }

        private static IReadOnlyList<IPlanningStrategy> CreateStrategies()
        {
            return new IPlanningStrategy[]
            {
                new SwitchLaneStrategy(),
                new JumpOverStrategy(),
                new SuperJumpOverStrategy(),
                new JumpOnStrategy(),
                new SuperJumpOnStrategy(),
                new JumpOnRoofStrategy(),
                new SuperJumpOnRoofStrategy(),
                new JumpOnFromRoofStrategy(),
                new SuperJumpOnFromRoofStrategy(),
                new JumpFromRoofStrategy(),
                new SuperJumpFromRoofStrategy(),
                new JumpFromRoofOnRoofStrategy(),
                new SuperJumpFromRoofOnRoofStrategy(),
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

#if UNITY_EDITOR
            long tickStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            TickBot();
#if UNITY_EDITOR
            RecordPerfSample(GetElapsedMilliseconds(tickStartTimestamp));
#endif
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
#if UNITY_EDITOR
            DisposePerfDiagnostics();
#endif
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
#if UNITY_EDITOR
            long snapshotStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            LastSnapshot = _snapshotBuilder.Build(_hamster);
#if UNITY_EDITOR
            _lastSnapshotBuildMs = GetElapsedMilliseconds(snapshotStartTimestamp);
            long executorStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            bool executionChanged = _executor.Tick(_hamster);
#if UNITY_EDITOR
            _lastExecutorTickMs = GetElapsedMilliseconds(executorStartTimestamp);
#endif

            // Переснимаем snapshot только после фактического execution-перехода.
            // В обычных кадрах без fire/complete/cancel исходный snapshot остаётся актуальным для replanning.
            if (executionChanged)
            {
#if UNITY_EDITOR
                snapshotStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                LastSnapshot = _snapshotBuilder.Build(_hamster);
#if UNITY_EDITOR
                _lastSnapshotBuildMs += GetElapsedMilliseconds(snapshotStartTimestamp);
#endif
            }

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

#if UNITY_EDITOR
            long planBuildStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            BotPlan plan = _planBuilder.Build(LastSnapshot, _executor.CurrentPlan, _executor.IsActionInProgress);
#if UNITY_EDITOR
            _lastPlanBuildMs = GetElapsedMilliseconds(planBuildStartTimestamp);
#endif
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

#if UNITY_EDITOR
        private void InitializePerfDiagnostics()
        {
            try
            {
                _gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC Allocated In Frame");
                _gcUsedMemoryRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC Used Memory");
                _perfDiagnosticsReady = true;
            }
            catch (Exception ex)
            {
                DebugManager.DiagStability($"[PERF INIT] ProfilerRecorder unavailable: {ex.Message}");
            }

            _nextPerfSummaryTime = Time.realtimeSinceStartup + PerfSummaryIntervalSeconds;
            _lastGen0CollectionCount = global::System.GC.CollectionCount(0);
            _lastGen1CollectionCount = global::System.GC.CollectionCount(1);
            _lastGen2CollectionCount = global::System.GC.CollectionCount(2);
        }

        private void DisposePerfDiagnostics()
        {
            if (_gcAllocatedInFrameRecorder.Valid)
                _gcAllocatedInFrameRecorder.Dispose();

            if (_gcUsedMemoryRecorder.Valid)
                _gcUsedMemoryRecorder.Dispose();
        }

        private void RecordPerfSample(float botTickMs)
        {
            if (!_perfDiagnosticsReady)
                return;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            long gcAllocBytes = _gcAllocatedInFrameRecorder.LastValue;

            _perfFrameCount++;
            _perfTotalGcAllocBytes += global::System.Math.Max(0L, gcAllocBytes);
            _perfMaxGcAllocBytes = global::System.Math.Max(_perfMaxGcAllocBytes, gcAllocBytes);
            _perfMaxFrameMs = global::System.Math.Max(_perfMaxFrameMs, frameMs);
            _perfMaxBotTickMs = global::System.Math.Max(_perfMaxBotTickMs, botTickMs);
            _perfMaxSnapshotMs = global::System.Math.Max(_perfMaxSnapshotMs, _lastSnapshotBuildMs);
            _perfMaxExecutorMs = global::System.Math.Max(_perfMaxExecutorMs, _lastExecutorTickMs);
            _perfMaxPlanBuildMs = global::System.Math.Max(_perfMaxPlanBuildMs, _lastPlanBuildMs);

            if (frameMs >= FrameSpikeThresholdMs
                || botTickMs >= BotTickSpikeThresholdMs
                || gcAllocBytes >= GcAllocSpikeThresholdBytes)
            {
                _perfSpikeFrameCount++;
                DebugManager.DiagStability(
                    $"[PERF SPIKE] frameMs={frameMs:F2} botTickMs={botTickMs:F2} " +
                    $"snapshotMs={_lastSnapshotBuildMs:F2} executorMs={_lastExecutorTickMs:F2} " +
                    $"planBuildMs={_lastPlanBuildMs:F2} gcAllocKB={gcAllocBytes / 1024f:F1} " +
                    $"gcUsedMB={_gcUsedMemoryRecorder.LastValue / (1024f * 1024f):F1} " +
                    $"obstacles={LastSnapshot?.Obstacles.Count ?? 0} planActions={CurrentPlan.Actions.Count}");
            }

            if (Time.realtimeSinceStartup >= _nextPerfSummaryTime)
                LogPerfSummary();
        }

        private void LogPerfSummary()
        {
            int gen0Count = global::System.GC.CollectionCount(0);
            int gen1Count = global::System.GC.CollectionCount(1);
            int gen2Count = global::System.GC.CollectionCount(2);
            int gen0Delta = gen0Count - _lastGen0CollectionCount;
            int gen1Delta = gen1Count - _lastGen1CollectionCount;
            int gen2Delta = gen2Count - _lastGen2CollectionCount;

            HelpMethods.ConsumeWorldShiftClipDiagnostics(
                out int clipCalls,
                out float clipTotalMs,
                out float clipMaxMs,
                out string clipMaxName);

            float avgGcAllocKb = _perfFrameCount > 0
                ? _perfTotalGcAllocBytes / 1024f / _perfFrameCount
                : 0f;

            DebugManager.DiagStability(
                $"[PERF SUMMARY] frames={_perfFrameCount} spikes={_perfSpikeFrameCount} " +
                $"maxFrameMs={_perfMaxFrameMs:F2} maxBotTickMs={_perfMaxBotTickMs:F2} " +
                $"maxSnapshotMs={_perfMaxSnapshotMs:F2} maxExecutorMs={_perfMaxExecutorMs:F2} " +
                $"maxPlanBuildMs={_perfMaxPlanBuildMs:F2} avgGcAllocKB={avgGcAllocKb:F1} " +
                $"maxGcAllocKB={_perfMaxGcAllocBytes / 1024f:F1} gcCollections={gen0Delta}/{gen1Delta}/{gen2Delta} " +
                $"gcUsedMB={_gcUsedMemoryRecorder.LastValue / (1024f * 1024f):F1} " +
                $"clipTravelCalls={clipCalls} clipTravelTotalMs={clipTotalMs:F2} " +
                $"clipTravelMaxMs={clipMaxMs:F2} clipTravelMax={clipMaxName ?? "-"} " +
                $"obstacles={LastSnapshot?.Obstacles.Count ?? 0} planActions={CurrentPlan.Actions.Count}");

            _perfFrameCount = 0;
            _perfSpikeFrameCount = 0;
            _perfTotalGcAllocBytes = 0;
            _perfMaxGcAllocBytes = 0;
            _perfMaxFrameMs = 0f;
            _perfMaxBotTickMs = 0f;
            _perfMaxSnapshotMs = 0f;
            _perfMaxExecutorMs = 0f;
            _perfMaxPlanBuildMs = 0f;
            _lastGen0CollectionCount = gen0Count;
            _lastGen1CollectionCount = gen1Count;
            _lastGen2CollectionCount = gen2Count;
            _nextPerfSummaryTime = Time.realtimeSinceStartup + PerfSummaryIntervalSeconds;
        }

        private static float GetElapsedMilliseconds(long startTimestamp)
        {
            long elapsedTicks = global::System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            return elapsedTicks * 1000f / global::System.Diagnostics.Stopwatch.Frequency;
        }
#endif
    }
}
