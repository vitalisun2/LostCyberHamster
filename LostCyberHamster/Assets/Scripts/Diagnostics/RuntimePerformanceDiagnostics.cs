using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Assets.Scripts.Bot.PlanState;
using UnityEngine;
using UnityEngine.Profiling;

namespace Assets.Scripts.Diagnostics
{
    public enum RuntimePerformanceScope
    {
        ObstacleSpawnerSpawnPatterns,
        ObstacleSpawnerIsPatternFullyOnScreen,
        ObstacleSpawnerGetNextPatternTargetLeftEdge,
        ObstacleSpawnerSpawnPattern,
        ObstacleSpawnerUnspawnObstacle,
        VisiblePatternTrackerUpdate,
        VisiblePatternTrackerPrune,
        GameUiUpdate,
        GameManagerUpdateLoop,
        GameManagerLateUpdateLoop,
        GameManagerUpdateObstacleListener,
        GameManagerUpdateHamsterListener,
        GameManagerUpdateScrollingEnvironmentListener,
        GameManagerUpdateGameUiListener,
        GameManagerUpdateObstacleSpawnerListener,
        GameManagerUpdateOtherListener,
        GameManagerLateUpdateRuntimeBotListener,
        GameManagerLateUpdateOtherListener,
        RuntimeBotLateUpdate,
        RuntimeBotTick,
        RuntimeBotSnapshotBuild,
        RuntimeBotExecutorTick,
        RuntimeBotApplyAsyncReplan,
        RuntimeBotStartAsyncReplan,
        RuntimeBotAsyncPlanBuild,
        RuntimeBotAsyncCreateStrategies,
        RuntimeBotAsyncCreateRebuilder,
        RuntimeBotAsyncRebuilderBuild,
        RuntimeBotAsyncBuildPlanForRequest,
        RuntimeBotAsyncBuildCommittedPrefix,
        RuntimeBotAsyncBuildTailRootState,
        RuntimeBotPlanBuilderBuild,
        RuntimeBotPlanningGraphBuildBranches,
        RuntimeBotActionGeneratorGenerate,
        RuntimeBotStrategyCollectActions,
        RuntimeBotPlanningSnapshotProjectorProject,
        RuntimeBotObstacleChainBuilderTryBuild,
        RuntimeBotTransitionSimulatorSimulate,
        RuntimeBotTransitionSimulatorProjectInProgress
    }

    public enum RuntimePerformanceCounter
    {
        PlanningStrategyResultNotApplicable,
        PlanningStrategyResultNoAction,
        PlanningStrategyResultFromActions,
        PlanningStrategyResultFromAction,
        PlanningStrategyResultDeadEnd,
        PlanningStrategyResultInsufficientEnergy,
        JumpObstacleProjectionBuildBaseCalls,
        JumpObstacleProjectionBuildBaseItems,
        JumpObstacleProjectionBuildShiftedCalls,
        JumpObstacleProjectionBuildShiftedItems,
        ObstacleRoleClassifierGetRolesCalls,
        ObstacleRoleClassifierEmptyRoleSets,
        ObstacleRoleClassifierNonEmptyRoleSets,
        ObstacleRoleClassifierAssignedRoles,
        ObstacleChainBuilderElementLists,
        ObstacleChainBuilderElementListItems,
        ObstacleChainConstructed,
        ObstacleChainCopiedElements,
        ObstacleChainElementConstructed,
        ObstacleChainElementCopiedRoles
    }

    public enum RuntimePerformanceFrameFlag
    {
        RuntimeBotAsyncPlanRunning
    }

    /// <summary>
    /// Легкая диагностика frame spikes для automation-прогонов.
    /// </summary>
    public static class RuntimePerformanceDiagnostics
    {
        private const float SpikeThresholdMs = 35f;

        private static bool _enabled;
        private static bool _summaryLogged;
        private static int _sampledFrames;
        private static int _spikeFrames;
        private static float _maxFrameMs;
        private static int _mainThreadId;
        private static int _startGc0;
        private static int _startGc1;
        private static int _startGc2;
        private static int _lastGc0;
        private static int _lastGc1;
        private static int _lastGc2;
        private static int _framesWithGc0;
        private static int _framesWithGc1;
        private static int _framesWithGc2;
        private static int _spikeFramesWithGc0;
        private static int _spikeFramesWithGc1;
        private static int _spikeFramesWithGc2;
        private static long[] _scopeAllocatedBytes = CreateScopeLongArray();
        private static long[] _scopeMaxAllocatedBytes = CreateScopeLongArray();
        private static int[] _scopeAllocationCalls = CreateScopeIntArray();
        private static long[] _scopeElapsedTicks = CreateScopeLongArray();
        private static long[] _scopeMaxElapsedTicks = CreateScopeLongArray();
        private static int[] _scopeTimingCalls = CreateScopeIntArray();
        private static long[] _frameScopeAllocatedBytes = CreateScopeLongArray();
        private static long[] _frameScopeElapsedTicks = CreateScopeLongArray();
        private static int[] _frameScopeCalls = CreateScopeIntArray();
        private static long[] _spikeScopeAllocatedBytes = CreateScopeLongArray();
        private static long[] _spikeScopeElapsedTicks = CreateScopeLongArray();
        private static int[] _spikeScopeCalls = CreateScopeIntArray();
        private static long[] _counterValues = CreateCounterLongArray();
        private static long[] _strategyCollectAllocatedBytes = CreateActionKindLongArray();
        private static long[] _strategyCollectMaxAllocatedBytes = CreateActionKindLongArray();
        private static int[] _strategyCollectAllocationCalls = CreateActionKindIntArray();
        private static long[] _strategyCollectElapsedTicks = CreateActionKindLongArray();
        private static long[] _strategyCollectMaxElapsedTicks = CreateActionKindLongArray();
        private static int[] _strategyCollectTimingCalls = CreateActionKindIntArray();
        private static bool[] _currentFrameFlags = CreateFrameFlagBoolArray();
        private static int[] _frameFlagFrames = CreateFrameFlagIntArray();
        private static int[] _frameFlagSpikeFrames = CreateFrameFlagIntArray();

        [ThreadStatic]
        private static long[] _threadScopeStartTicks;
        [ThreadStatic]
        private static long _threadStrategyCollectStartTicks;

        /// <summary>
        /// Включает монитор только для automation-запуска, чтобы обычный gameplay не получал лишних замеров.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled)
                return;

            _enabled = enabled;
            ResetCounters();

            if (_enabled)
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                DebugManager.DiagStability(
                    $"[PERF] monitor enabled thresholdMs={SpikeThresholdMs:0.0} mode=summary-only");
            }
        }

        /// <summary>
        /// Делает один дешевый замер кадра без записи в файл во время gameplay.
        /// </summary>
        public static void SampleFrame()
        {
            if (!_enabled)
                return;

            _sampledFrames++;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            bool isSpike = frameMs >= SpikeThresholdMs;
            RecordFrameGcCounters(isSpike);
            RecordFrameFlagCounters(isSpike);
            if (frameMs > _maxFrameMs)
                _maxFrameMs = frameMs;

            if (!isSpike)
            {
                ClearCurrentFrameCounters();
                return;
            }

            _spikeFrames++;
            AddCurrentFrameToSpikeCounters();
            ClearCurrentFrameCounters();
        }

        /// <summary>
        /// Пишет итоговую строку performance-монитора при завершении или остановке automation-прогона.
        /// </summary>
        public static void LogSummary(string reason)
        {
            if (!_enabled || _summaryLogged)
                return;

            _summaryLogged = true;
            DebugManager.DiagStability(
                $"[PERF] summary reason={reason} frames={_sampledFrames} " +
                $"spikes={_spikeFrames} maxFrameMs={_maxFrameMs:0.0} " +
                $"{BuildGcSummary()} " +
                $"scopes={BuildScopeSummary()} " +
                $"spikeScopes={BuildSpikeScopeSummary()} " +
                $"counters={BuildCounterSummary()} " +
                $"strategyCollect={BuildStrategyCollectSummary()} " +
                $"frameFlags={BuildFrameFlagSummary()}");
        }

        public static void MarkFrameFlag(RuntimePerformanceFrameFlag flag)
        {
            if (!_enabled || Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return;

            _currentFrameFlags[(int)flag] = true;
        }

        /// <summary>
        /// Запоминает текущий allocation counter потока перед подозрительным runtime-блоком.
        /// </summary>
        public static long BeginAllocationSample(RuntimePerformanceScope scope)
        {
            if (!_enabled)
                return -1L;

            int scopeIndex = (int)scope;
            EnsureThreadScopeStartTicks();
            _threadScopeStartTicks[scopeIndex] = Stopwatch.GetTimestamp();

            return IsMainThread()
                ? Profiler.GetMonoUsedSizeLong()
                : GC.GetTotalMemory(forceFullCollection: false);
        }

        public static long BeginStrategyCollectSample(BotActionKind actionKind)
        {
            if (!_enabled)
                return -1L;

            _threadStrategyCollectStartTicks = Stopwatch.GetTimestamp();
            return IsMainThread()
                ? Profiler.GetMonoUsedSizeLong()
                : GC.GetTotalMemory(forceFullCollection: false);
        }

        /// <summary>
        /// Добавляет allocation delta блока в агрегированный proof-summary.
        /// </summary>
        public static void EndAllocationSample(RuntimePerformanceScope scope, long startBytes)
        {
            if (!_enabled || startBytes < 0L)
                return;

            int scopeIndex = (int)scope;
            long elapsedTicks = 0L;
            if (_threadScopeStartTicks != null)
            {
                long startTicks = _threadScopeStartTicks[scopeIndex];
                if (startTicks > 0L)
                {
                    elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                    _threadScopeStartTicks[scopeIndex] = 0L;
                }
            }

            long allocatedBytes = IsMainThread()
                ? Profiler.GetMonoUsedSizeLong() - startBytes
                : GC.GetTotalMemory(forceFullCollection: false) - startBytes;

            if (elapsedTicks > 0L)
            {
                Interlocked.Add(ref _scopeElapsedTicks[scopeIndex], elapsedTicks);
                Interlocked.Increment(ref _scopeTimingCalls[scopeIndex]);
                UpdateScopeMax(ref _scopeMaxElapsedTicks[scopeIndex], elapsedTicks);
            }

            if (allocatedBytes > 0L)
            {
                Interlocked.Add(ref _scopeAllocatedBytes[scopeIndex], allocatedBytes);
                Interlocked.Increment(ref _scopeAllocationCalls[scopeIndex]);
                UpdateScopeMax(ref _scopeMaxAllocatedBytes[scopeIndex], allocatedBytes);
            }

            if (!IsMainThread())
                return;

            if (elapsedTicks > 0L)
            {
                _frameScopeElapsedTicks[scopeIndex] += elapsedTicks;
                _frameScopeCalls[scopeIndex]++;
            }

            if (allocatedBytes > 0L)
                _frameScopeAllocatedBytes[scopeIndex] += allocatedBytes;
        }

        public static void EndStrategyCollectSample(BotActionKind actionKind, long startBytes)
        {
            if (!_enabled || startBytes < 0L)
                return;

            int scopeIndex = (int)RuntimePerformanceScope.RuntimeBotStrategyCollectActions;
            int actionKindIndex = (int)actionKind;
            long elapsedTicks = 0L;
            if (_threadStrategyCollectStartTicks > 0L)
            {
                elapsedTicks = Stopwatch.GetTimestamp() - _threadStrategyCollectStartTicks;
                _threadStrategyCollectStartTicks = 0L;
            }

            long allocatedBytes = IsMainThread()
                ? Profiler.GetMonoUsedSizeLong() - startBytes
                : GC.GetTotalMemory(forceFullCollection: false) - startBytes;

            if (elapsedTicks > 0L)
            {
                Interlocked.Add(ref _scopeElapsedTicks[scopeIndex], elapsedTicks);
                Interlocked.Increment(ref _scopeTimingCalls[scopeIndex]);
                UpdateScopeMax(ref _scopeMaxElapsedTicks[scopeIndex], elapsedTicks);

                Interlocked.Add(ref _strategyCollectElapsedTicks[actionKindIndex], elapsedTicks);
                Interlocked.Increment(ref _strategyCollectTimingCalls[actionKindIndex]);
                UpdateScopeMax(ref _strategyCollectMaxElapsedTicks[actionKindIndex], elapsedTicks);
            }

            if (allocatedBytes > 0L)
            {
                Interlocked.Add(ref _scopeAllocatedBytes[scopeIndex], allocatedBytes);
                Interlocked.Increment(ref _scopeAllocationCalls[scopeIndex]);
                UpdateScopeMax(ref _scopeMaxAllocatedBytes[scopeIndex], allocatedBytes);

                Interlocked.Add(ref _strategyCollectAllocatedBytes[actionKindIndex], allocatedBytes);
                Interlocked.Increment(ref _strategyCollectAllocationCalls[actionKindIndex]);
                UpdateScopeMax(ref _strategyCollectMaxAllocatedBytes[actionKindIndex], allocatedBytes);
            }

            if (!IsMainThread())
                return;

            if (elapsedTicks > 0L)
            {
                _frameScopeElapsedTicks[scopeIndex] += elapsedTicks;
                _frameScopeCalls[scopeIndex]++;
            }

            if (allocatedBytes > 0L)
                _frameScopeAllocatedBytes[scopeIndex] += allocatedBytes;
        }

        private static void ResetCounters()
        {
            _summaryLogged = false;
            _sampledFrames = 0;
            _spikeFrames = 0;
            _maxFrameMs = 0f;
            _startGc0 = GC.CollectionCount(0);
            _startGc1 = GC.CollectionCount(1);
            _startGc2 = GC.CollectionCount(2);
            Array.Clear(_scopeAllocatedBytes, 0, _scopeAllocatedBytes.Length);
            Array.Clear(_scopeMaxAllocatedBytes, 0, _scopeMaxAllocatedBytes.Length);
            Array.Clear(_scopeAllocationCalls, 0, _scopeAllocationCalls.Length);
            Array.Clear(_scopeElapsedTicks, 0, _scopeElapsedTicks.Length);
            Array.Clear(_scopeMaxElapsedTicks, 0, _scopeMaxElapsedTicks.Length);
            Array.Clear(_scopeTimingCalls, 0, _scopeTimingCalls.Length);
            _lastGc0 = _startGc0;
            _lastGc1 = _startGc1;
            _lastGc2 = _startGc2;
            _framesWithGc0 = 0;
            _framesWithGc1 = 0;
            _framesWithGc2 = 0;
            _spikeFramesWithGc0 = 0;
            _spikeFramesWithGc1 = 0;
            _spikeFramesWithGc2 = 0;
            ClearCurrentFrameCounters();
            Array.Clear(_spikeScopeAllocatedBytes, 0, _spikeScopeAllocatedBytes.Length);
            Array.Clear(_spikeScopeElapsedTicks, 0, _spikeScopeElapsedTicks.Length);
            Array.Clear(_spikeScopeCalls, 0, _spikeScopeCalls.Length);
            Array.Clear(_counterValues, 0, _counterValues.Length);
            Array.Clear(_strategyCollectAllocatedBytes, 0, _strategyCollectAllocatedBytes.Length);
            Array.Clear(_strategyCollectMaxAllocatedBytes, 0, _strategyCollectMaxAllocatedBytes.Length);
            Array.Clear(_strategyCollectAllocationCalls, 0, _strategyCollectAllocationCalls.Length);
            Array.Clear(_strategyCollectElapsedTicks, 0, _strategyCollectElapsedTicks.Length);
            Array.Clear(_strategyCollectMaxElapsedTicks, 0, _strategyCollectMaxElapsedTicks.Length);
            Array.Clear(_strategyCollectTimingCalls, 0, _strategyCollectTimingCalls.Length);
            Array.Clear(_frameFlagFrames, 0, _frameFlagFrames.Length);
            Array.Clear(_frameFlagSpikeFrames, 0, _frameFlagSpikeFrames.Length);
        }

        private static string BuildGcSummary()
        {
            return $"gc0={GC.CollectionCount(0) - _startGc0} " +
                   $"gc1={GC.CollectionCount(1) - _startGc1} " +
                   $"gc2={GC.CollectionCount(2) - _startGc2} " +
                $"gcFrames=gc0:{_framesWithGc0}/spike:{_spikeFramesWithGc0}," +
                $"gc1:{_framesWithGc1}/spike:{_spikeFramesWithGc1}," +
                $"gc2:{_framesWithGc2}/spike:{_spikeFramesWithGc2}";
        }

        public static void Count(RuntimePerformanceCounter counter)
        {
            if (!_enabled)
                return;

            Interlocked.Increment(ref _counterValues[(int)counter]);
        }

        public static void Count(RuntimePerformanceCounter counter, long value)
        {
            if (!_enabled || value == 0L)
                return;

            Interlocked.Add(ref _counterValues[(int)counter], value);
        }

        private static void UpdateScopeMax(ref long target, long value)
        {
            long currentMax;
            do
            {
                currentMax = Volatile.Read(ref target);
                if (value <= currentMax)
                    return;
            } while (Interlocked.CompareExchange(
                         ref target,
                         value,
                         currentMax) != currentMax);
        }

        private static void AddCurrentFrameToSpikeCounters()
        {
            for (int scopeIndex = 0; scopeIndex < _frameScopeElapsedTicks.Length; scopeIndex++)
            {
                long elapsedTicks = _frameScopeElapsedTicks[scopeIndex];
                long allocatedBytes = _frameScopeAllocatedBytes[scopeIndex];
                int calls = _frameScopeCalls[scopeIndex];
                if (elapsedTicks <= 0L && allocatedBytes <= 0L && calls <= 0)
                    continue;

                _spikeScopeElapsedTicks[scopeIndex] += elapsedTicks;
                _spikeScopeAllocatedBytes[scopeIndex] += allocatedBytes;
                _spikeScopeCalls[scopeIndex] += calls;
            }
        }

        private static void ClearCurrentFrameCounters()
        {
            Array.Clear(_frameScopeAllocatedBytes, 0, _frameScopeAllocatedBytes.Length);
            Array.Clear(_frameScopeElapsedTicks, 0, _frameScopeElapsedTicks.Length);
            Array.Clear(_frameScopeCalls, 0, _frameScopeCalls.Length);
            Array.Clear(_currentFrameFlags, 0, _currentFrameFlags.Length);
        }

        private static void RecordFrameGcCounters(bool isSpike)
        {
            int currentGc0 = GC.CollectionCount(0);
            int currentGc1 = GC.CollectionCount(1);
            int currentGc2 = GC.CollectionCount(2);

            if (currentGc0 > _lastGc0)
            {
                _framesWithGc0++;
                if (isSpike)
                    _spikeFramesWithGc0++;
            }

            if (currentGc1 > _lastGc1)
            {
                _framesWithGc1++;
                if (isSpike)
                    _spikeFramesWithGc1++;
            }

            if (currentGc2 > _lastGc2)
            {
                _framesWithGc2++;
                if (isSpike)
                    _spikeFramesWithGc2++;
            }

            _lastGc0 = currentGc0;
            _lastGc1 = currentGc1;
            _lastGc2 = currentGc2;
        }

        private static void RecordFrameFlagCounters(bool isSpike)
        {
            for (int flagIndex = 0; flagIndex < _currentFrameFlags.Length; flagIndex++)
            {
                if (!_currentFrameFlags[flagIndex])
                    continue;

                _frameFlagFrames[flagIndex]++;
                if (isSpike)
                    _frameFlagSpikeFrames[flagIndex]++;
            }
        }

        private static string BuildScopeSummary()
        {
            var builder = new StringBuilder();
            for (int scopeIndex = 0; scopeIndex < _scopeAllocatedBytes.Length; scopeIndex++)
            {
                long totalBytes = Volatile.Read(ref _scopeAllocatedBytes[scopeIndex]);
                long elapsedTicks = Volatile.Read(ref _scopeElapsedTicks[scopeIndex]);
                if (totalBytes <= 0L && elapsedTicks <= 0L)
                    continue;

                if (builder.Length > 0)
                    builder.Append("|");

                builder
                    .Append((RuntimePerformanceScope)scopeIndex)
                    .Append(":totalMs=")
                    .Append(FormatMs(elapsedTicks))
                    .Append(",calls=")
                    .Append(Volatile.Read(ref _scopeTimingCalls[scopeIndex]))
                    .Append(",maxMs=")
                    .Append(FormatMs(Volatile.Read(ref _scopeMaxElapsedTicks[scopeIndex])))
                    .Append(",totalKb=")
                    .Append((totalBytes / 1024f).ToString("0.0"))
                    .Append(",allocCalls=")
                    .Append(Volatile.Read(ref _scopeAllocationCalls[scopeIndex]))
                    .Append(",maxKb=")
                    .Append((Volatile.Read(ref _scopeMaxAllocatedBytes[scopeIndex]) / 1024f).ToString("0.0"));
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static string BuildFrameFlagSummary()
        {
            var builder = new StringBuilder();
            for (int flagIndex = 0; flagIndex < _frameFlagFrames.Length; flagIndex++)
            {
                int frames = _frameFlagFrames[flagIndex];
                int spikeFrames = _frameFlagSpikeFrames[flagIndex];
                if (frames <= 0 && spikeFrames <= 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append("|");

                builder
                    .Append((RuntimePerformanceFrameFlag)flagIndex)
                    .Append(":frames=")
                    .Append(frames)
                    .Append(",spikes=")
                    .Append(spikeFrames);
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static string BuildCounterSummary()
        {
            var builder = new StringBuilder();
            for (int counterIndex = 0; counterIndex < _counterValues.Length; counterIndex++)
            {
                long value = Volatile.Read(ref _counterValues[counterIndex]);
                if (value <= 0L)
                    continue;

                if (builder.Length > 0)
                    builder.Append("|");

                builder
                    .Append((RuntimePerformanceCounter)counterIndex)
                    .Append(":")
                    .Append(value);
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static string BuildStrategyCollectSummary()
        {
            var builder = new StringBuilder();
            for (int actionKindIndex = 0; actionKindIndex < _strategyCollectAllocatedBytes.Length; actionKindIndex++)
            {
                long totalBytes = Volatile.Read(ref _strategyCollectAllocatedBytes[actionKindIndex]);
                long elapsedTicks = Volatile.Read(ref _strategyCollectElapsedTicks[actionKindIndex]);
                if (totalBytes <= 0L && elapsedTicks <= 0L)
                    continue;

                if (builder.Length > 0)
                    builder.Append("|");

                builder
                    .Append((BotActionKind)actionKindIndex)
                    .Append(":totalMs=")
                    .Append(FormatMs(elapsedTicks))
                    .Append(",calls=")
                    .Append(Volatile.Read(ref _strategyCollectTimingCalls[actionKindIndex]))
                    .Append(",maxMs=")
                    .Append(FormatMs(Volatile.Read(ref _strategyCollectMaxElapsedTicks[actionKindIndex])))
                    .Append(",totalKb=")
                    .Append((totalBytes / 1024f).ToString("0.0"))
                    .Append(",allocCalls=")
                    .Append(Volatile.Read(ref _strategyCollectAllocationCalls[actionKindIndex]))
                    .Append(",maxKb=")
                    .Append((Volatile.Read(ref _strategyCollectMaxAllocatedBytes[actionKindIndex]) / 1024f).ToString("0.0"));
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static string BuildSpikeScopeSummary()
        {
            var builder = new StringBuilder();
            for (int scopeIndex = 0; scopeIndex < _spikeScopeElapsedTicks.Length; scopeIndex++)
            {
                long elapsedTicks = _spikeScopeElapsedTicks[scopeIndex];
                long totalBytes = _spikeScopeAllocatedBytes[scopeIndex];
                int calls = _spikeScopeCalls[scopeIndex];
                if (elapsedTicks <= 0L && totalBytes <= 0L && calls <= 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append("|");

                builder
                    .Append((RuntimePerformanceScope)scopeIndex)
                    .Append(":spikeMs=")
                    .Append(FormatMs(elapsedTicks))
                    .Append(",calls=")
                    .Append(calls)
                    .Append(",spikeKb=")
                    .Append((totalBytes / 1024f).ToString("0.0"));
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static string FormatMs(long ticks)
        {
            return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.000");
        }

        private static void EnsureThreadScopeStartTicks()
        {
            _threadScopeStartTicks ??= CreateScopeLongArray();
        }

        private static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        private static long[] CreateScopeLongArray()
        {
            return new long[Enum.GetValues(typeof(RuntimePerformanceScope)).Length];
        }

        private static int[] CreateScopeIntArray()
        {
            return new int[Enum.GetValues(typeof(RuntimePerformanceScope)).Length];
        }

        private static long[] CreateCounterLongArray()
        {
            return new long[Enum.GetValues(typeof(RuntimePerformanceCounter)).Length];
        }

        private static long[] CreateActionKindLongArray()
        {
            return new long[Enum.GetValues(typeof(BotActionKind)).Length];
        }

        private static int[] CreateActionKindIntArray()
        {
            return new int[Enum.GetValues(typeof(BotActionKind)).Length];
        }

        private static bool[] CreateFrameFlagBoolArray()
        {
            return new bool[Enum.GetValues(typeof(RuntimePerformanceFrameFlag)).Length];
        }

        private static int[] CreateFrameFlagIntArray()
        {
            return new int[Enum.GetValues(typeof(RuntimePerformanceFrameFlag)).Length];
        }
    }
}
