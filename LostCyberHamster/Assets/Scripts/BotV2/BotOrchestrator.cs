using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
using Sirenix.OdinInspector;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Оркестратор BotV2. Вешается на GameObject в сцене.
    /// Координирует pipeline: Snapshot → Classify → Generate → Select → Execute.
    /// Горячая клавиша F1: вкл/выкл.
    /// Этап 5: event-driven pipeline на триггерах видимости и завершения шага.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
        [Flags]
        private enum PipelineTrigger
        {
            None = 0,
            VisibleObjectsChanged = 1 << 0,
            StepCompleted = 1 << 1,
            StepCancelled = 1 << 2,
            ManagedStateChanged = 1 << 3
        }

        [Title("BotV2 Settings")]
        [SerializeField] private bool _enabledOnStart = true;

        [SerializeField, Range(5f, 30f)]
        [Tooltip("Дальность сканирования (мировых единиц)")]
        private float _scanRange = 15f;

        [SerializeField]
        public BotLogLevel LogLevel = BotLogLevel.Normal;

        [Title("Runtime"), ReadOnly]
        [ShowInInspector] public bool IsEnabled { get; private set; }

        [ShowInInspector, ReadOnly]
        private string _lastAction = "—";

        // ──────── Pipeline ────────

        private Hamster      _hamster;
        private GameManager  _gameManager;

        private SnapshotBuilder  _snapshotBuilder;
        private ObjectClassifier _classifier;
        private ActionGenerator  _generator;
        private ActionSelector   _selector;
        private StepExecutor     _executor;

        private bool      _initialized;
        private ChainStep _activeStep;
        private float _nextInitRetryTime;
        private BotSceneSnapshot _lastSnapshot;
        private float _nextInitFailureLogTime;
        private string _lastNoActionSignature;
        private float _lastNoActionLogTime = -999f;
        private int _suppressedNoActionCount;
        private int _blockedSwitchLaneStableId;
        private PipelineTrigger _pendingPipelineTrigger;
        private bool _hasVisibleObjectSnapshot;
        private readonly HashSet<int> _currentVisibleObjectIds = new HashSet<int>();
        private readonly HashSet<int> _previousVisibleObjectIds = new HashSet<int>();
        private bool _hasLastManagedState;
        private HamsterStateEnum _lastManagedState;
        private bool _hasPlannedManagedState;
        private HamsterStateEnum _plannedManagedState;

        // ──────── Lifecycle ────────

        private void Start()
        {
            if (!BotV2Bootstrap.UseBotV2AsPrimary)
            {
                enabled = false;
                return;
            }

            if (_enabledOnStart)
                EnableAsPrimary();
        }

        /// <summary>
        /// Включает BotV2 в режиме основного бота.
        /// Вызывается bootstrap-инициализацией после загрузки сцены.
        /// </summary>
        public void EnableAsPrimary()
        {
            enabled = true;
            IsEnabled = true;
            ResetPipelineTriggerState();

            if (!_initialized)
                TryInit();
        }

        private void Update()
        {
            if (IsTogglePressed()) Toggle();

            if (!IsEnabled) return;

            // BotV2 может создаваться в Bootstrap-сцене раньше, чем появятся Hamster/GameManager.
            // Повторяем TryInit до успешной инициализации.
            if (!_initialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryInit();
                    _nextInitRetryTime = Time.time + 0.5f;
                }
                return;
            }

            if (_gameManager == null || _gameManager.State != GameState.PLAYING) return;

            BotLogger.Level = LogLevel;

            var state = _hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Dead) return;

            var snapshot = _snapshotBuilder.Build(_hamster, _scanRange);
            _lastSnapshot = snapshot;
            RefreshBlockedSwitchLane(snapshot);

            if (UpdateVisibleObjectSet(snapshot))
                QueuePipelineTrigger(PipelineTrigger.VisibleObjectsChanged);

            bool managedStateChanged = UpdateManagedState(state);

            if (UpdateActiveStep(state, managedStateChanged))
                return;

            state = _hamster.HamsterState.Value;
            if (!IsManagedState(state))
                return;

            if (managedStateChanged)
                QueuePipelineTrigger(PipelineTrigger.ManagedStateChanged);

            var trigger = ConsumePipelineTrigger();
            if (trigger == PipelineTrigger.None)
                return;

            RunPipeline(snapshot, trigger);
        }

        private static bool IsTogglePressed()
        {
        #if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
        #elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F1);
        #else
            return false;
        #endif
        }

        // ──────── Pipeline ────────

        private void RunPipeline(BotSceneSnapshot snapshot, PipelineTrigger trigger)
        {
            BotLogger.Log(BotLogLevel.Normal,
                $"[PIPELINE] trigger={FormatPipelineTrigger(trigger)}\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  visible: {BotLogger.FormatSnapshotObstacles(snapshot.VisibleObjects)}");

            LogSnapshot(snapshot);

            _classifier.Classify(snapshot);
            LogClassify(snapshot);

            var candidates = _generator.Generate(snapshot);
            FilterBlockedSwitchLaneCandidates(candidates);
            LogGenerate(candidates);

            var best = _selector.Select(candidates);
            if (best == null)
            {
                _lastAction = "None";
                LogNoSafeActions(snapshot);
                return;
            }

            ResetNoActionLogState();
            _lastAction = best.Action.ToString();
            BotLogger.Log(BotLogLevel.Normal,
                $"[SELECT] chose {best.Action} (cost={best.EnergyCost}, reason=\"{best.Reason}\")\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  step: {BotLogger.FormatStep(best)}\n" +
                $"  visible: {BotLogger.FormatSnapshotObstacles(snapshot.VisibleObjects)}");

            _activeStep = best;
            _plannedManagedState = _hamster.HamsterState.Value;
            _hasPlannedManagedState = IsManagedState(_plannedManagedState);
            _executor.SetStep(best);
        }

        // ──────── Init / Toggle ────────

        private void Toggle()
        {
            if (IsEnabled)
            {
                IsEnabled = false;
                ResetPipelineTriggerState();
                DebugManager.DiagLog("[BotOrchestrator] Disabled");
                return;
            }
            IsEnabled = true;
            ResetPipelineTriggerState();
            if (!_initialized) TryInit();
        }

        /// <summary>
        /// Переключение бота через глобальный хоткей (F1) из KeyboardMechanics.
        /// </summary>
        public void ToggleEnabledFromHotkey()
        {
            Toggle();
        }

        private void TryInit()
        {
            _hamster     = FindObjectOfType<Hamster>();
            _gameManager = FindObjectOfType<GameManager>();

            if (_hamster == null || _gameManager == null)
            {
                if (Time.time >= _nextInitFailureLogTime)
                {
                    DebugManager.DiagLog("[BotOrchestrator] Init failed — Hamster or GameManager not found");
                    _nextInitFailureLogTime = Time.time + 2f;
                }
                return;
            }

            _snapshotBuilder = new SnapshotBuilder();
            _classifier      = new ObjectClassifier();
            _generator       = new ActionGenerator();
            _selector        = new ActionSelector();
            _executor        = new StepExecutor(_hamster);

            // Логирование урона
            _hamster.DamageEvent.Subscribe(OnDamage);
            _gameManager.OnFinish += OnGameFinished;
            GameEventsManager.OnLevelCompleted += OnLevelCompleted;

            _initialized = true;
            IsEnabled    = true;
            ResetPipelineTriggerState();
            DebugManager.DiagLog("[BotOrchestrator] Initialized (Stage 5)");
        }

        private void OnDestroy()
        {
            if (_hamster != null)
                _hamster.DamageEvent.Unsubscribe(OnDamage);

            if (_gameManager != null)
                _gameManager.OnFinish -= OnGameFinished;

            GameEventsManager.OnLevelCompleted -= OnLevelCompleted;
        }

        private void RememberCancelledSwitchLane()
        {
            if (_activeStep == null || _activeStep.Action != BotAction.SwitchLane)
                return;

            if (_activeStep.TargetObstacle.Category == ObjectCategory.Collectible)
                return;

            int stableId = _activeStep.TargetObstacle.StableId;
            if (stableId == 0 || stableId == _blockedSwitchLaneStableId)
                return;

            _blockedSwitchLaneStableId = stableId;
            BotLogger.Log(BotLogLevel.Normal,
                $"[SELECT] block SwitchLane for obstacle id={stableId} after live cancellation");
        }

        private void RefreshBlockedSwitchLane(BotSceneSnapshot snapshot)
        {
            if (_blockedSwitchLaneStableId == 0)
                return;

            for (int index = 0; index < snapshot.VisibleObjects.Count; index++)
            {
                var obstacle = snapshot.VisibleObjects[index];
                if (obstacle.StableId != _blockedSwitchLaneStableId)
                    continue;

                if (obstacle.DistanceToHamster >= 0f)
                    return;

                break;
            }

            _blockedSwitchLaneStableId = 0;
        }

        private void FilterBlockedSwitchLaneCandidates(List<ChainStep> candidates)
        {
            if (_blockedSwitchLaneStableId == 0)
                return;

            candidates.RemoveAll(candidate =>
                candidate.Action == BotAction.SwitchLane &&
                candidate.TargetObstacle.StableId == _blockedSwitchLaneStableId);
        }

        private bool UpdateActiveStep(HamsterStateEnum state, bool managedStateChanged)
        {
            if (_activeStep == null)
                return false;

            if (_activeStep.Status == ChainStepStatus.Ready &&
                managedStateChanged &&
                _hasPlannedManagedState &&
                IsManagedState(state) &&
                state != _plannedManagedState)
            {
                BotLogger.Log(BotLogLevel.Normal,
                    $"[PIPELINE] invalidate ready step after state change {_plannedManagedState}→{state}\n" +
                    $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                    $"  step: {BotLogger.FormatStep(_activeStep)}");
                CompleteActiveStep(PipelineTrigger.ManagedStateChanged);
                return false;
            }

            if (_activeStep.Status == ChainStepStatus.Completed)
            {
                CompleteActiveStep(PipelineTrigger.StepCompleted);
                return false;
            }

            _executor.TryExecute();
            if (_executor.WasCancelled)
            {
                RememberCancelledSwitchLane();
                CompleteActiveStep(PipelineTrigger.StepCancelled);
                return false;
            }

            if (_activeStep.Status == ChainStepStatus.Completed)
            {
                CompleteActiveStep(PipelineTrigger.StepCompleted);
                return false;
            }

            return true;
        }

        private void CompleteActiveStep(PipelineTrigger trigger)
        {
            _executor.ClearStep();
            _activeStep = null;
            _hasPlannedManagedState = false;
            QueuePipelineTrigger(trigger);
        }

        private bool UpdateVisibleObjectSet(BotSceneSnapshot snapshot)
        {
            _currentVisibleObjectIds.Clear();
            for (int index = 0; index < snapshot.VisibleObjects.Count; index++)
                _currentVisibleObjectIds.Add(snapshot.VisibleObjects[index].StableId);

            if (!_hasVisibleObjectSnapshot)
            {
                CopyVisibleObjectIds(_currentVisibleObjectIds, _previousVisibleObjectIds);
                _hasVisibleObjectSnapshot = true;
                return true;
            }

            if (_previousVisibleObjectIds.SetEquals(_currentVisibleObjectIds))
                return false;

            CopyVisibleObjectIds(_currentVisibleObjectIds, _previousVisibleObjectIds);
            return true;
        }

        private void QueuePipelineTrigger(PipelineTrigger trigger)
        {
            if (trigger == PipelineTrigger.None)
                return;

            _pendingPipelineTrigger |= trigger;
        }

        private PipelineTrigger ConsumePipelineTrigger()
        {
            var trigger = _pendingPipelineTrigger;
            _pendingPipelineTrigger = PipelineTrigger.None;
            return trigger;
        }

        private void ResetPipelineTriggerState()
        {
            _pendingPipelineTrigger = PipelineTrigger.None;
            _hasVisibleObjectSnapshot = false;
            _currentVisibleObjectIds.Clear();
            _previousVisibleObjectIds.Clear();
            _hasLastManagedState = false;
            _hasPlannedManagedState = false;
            ResetNoActionLogState();
        }

        private bool UpdateManagedState(HamsterStateEnum state)
        {
            if (!IsManagedState(state))
                return false;

            if (!_hasLastManagedState)
            {
                _lastManagedState = state;
                _hasLastManagedState = true;
                return false;
            }

            if (_lastManagedState == state)
                return false;

            _lastManagedState = state;
            return true;
        }

        private static void CopyVisibleObjectIds(HashSet<int> source, HashSet<int> destination)
        {
            destination.Clear();
            foreach (int stableId in source)
                destination.Add(stableId);
        }

        private static bool IsManagedState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.Run || state == HamsterStateEnum.RoofRun;
        }

        private static string FormatPipelineTrigger(PipelineTrigger trigger)
        {
            if (trigger == PipelineTrigger.None)
                return "None";

            var builder = new StringBuilder();
            AppendTriggerName(builder, trigger, PipelineTrigger.VisibleObjectsChanged, "VisibleObjectsChanged");
            AppendTriggerName(builder, trigger, PipelineTrigger.StepCompleted, "StepCompleted");
            AppendTriggerName(builder, trigger, PipelineTrigger.StepCancelled, "StepCancelled");
            AppendTriggerName(builder, trigger, PipelineTrigger.ManagedStateChanged, "ManagedStateChanged");
            return builder.ToString();
        }

        private static void AppendTriggerName(
            StringBuilder builder,
            PipelineTrigger trigger,
            PipelineTrigger flag,
            string name)
        {
            if ((trigger & flag) == 0)
                return;

            if (builder.Length > 0)
                builder.Append('+');

            builder.Append(name);
        }

        // ──────── Damage log ────────

        private void OnDamage()
        {
            var step = _activeStep;
            string killerInfo = FindNearestThreatInfo();

            DebugManager.DiagLog(
                $"[DAMAGE] ===\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  active step: {BotLogger.FormatStep(step)}\n" +
                $"  last snapshot: {BotLogger.FormatSnapshotObstacles(_lastSnapshot?.VisibleObjects)}\n" +
                $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, step?.TargetObstacle.StableId ?? 0)}\n" +
                $"  killer (nearest same-lane threat): {killerInfo}\n" +
                $"[DAMAGE] ===");

            if (_hamster.Lives.Value <= 0)
            {
                DebugManager.DiagLog(
                    $"[TEST RESULT] FAIL\n" +
                    $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                    $"  active step: {BotLogger.FormatStep(step)}\n" +
                    $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, step?.TargetObstacle.StableId ?? 0)}");
            }
        }

        private void OnGameFinished()
        {
            string finishWarning = _activeStep != null || _hamster.HamsterState.Value != HamsterStateEnum.Run
                ? $"\n  warning: finish while state={_hamster.HamsterState.Value} activeStep={BotLogger.FormatStep(_activeStep)}"
                : string.Empty;

            DebugManager.DiagLog(
                $"[TEST FINISH] state={_gameManager.State} lastAction={_lastAction}\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}{finishWarning}\n" +
                $"  last snapshot: {BotLogger.FormatSnapshotObstacles(_lastSnapshot?.VisibleObjects)}");
        }

        private void OnLevelCompleted(int levelId, int stars)
        {
            string finishWarning = _activeStep != null || _hamster.HamsterState.Value != HamsterStateEnum.Run
                ? $"\n  warning: completed while state={_hamster.HamsterState.Value} activeStep={BotLogger.FormatStep(_activeStep)}"
                : string.Empty;

            DebugManager.DiagLog(
                $"[TEST RESULT] WIN level={levelId} stars={stars}\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}{finishWarning}\n" +
                $"  last snapshot: {BotLogger.FormatSnapshotObstacles(_lastSnapshot?.VisibleObjects)}");
        }

        private string FindNearestThreatInfo()
        {
            var spawner = Assets.Scripts.System.ObstacleSpawner.Instance;
            if (spawner == null) return "spawner=null";

            bool hamsterOnBottom = _hamster.IsOnBottomLine.Value;
            float hamsterRightX = _hamster.RightX;

            string nearestSameLane = "none";
            float minDistSame = float.MaxValue;
            string nearestAnyLane = "none";
            float minDistAny = float.MaxValue;

            foreach (var inst in spawner.SpawnedObstacles)
            {
                if (inst?.ObstacleScript == null) continue;
                var obs = inst.ObstacleScript;
                if (obs.ObstacleType.ObstacleTypeEnum !=
                    Assets.Scripts.Common.Models.ObstacleTypeEnum.smallNotAliveRoad) continue;

                float leftX = obs.transform.position.x - obs.ColliderWidth * 0.5f;
                float dist = leftX - hamsterRightX;
                string lane = obs.ObstacleType.IsTop ? "top" : "bottom";
                string info = $"{obs.ObstacleType.ObstacleTypeEnum} lane={lane} dist={dist:F2}";

                if (Mathf.Abs(dist) < Mathf.Abs(minDistAny))
                {
                    minDistAny = dist;
                    nearestAnyLane = info;
                }

                bool sameLane = (hamsterOnBottom && !obs.ObstacleType.IsTop) ||
                                (!hamsterOnBottom && obs.ObstacleType.IsTop);
                if (sameLane && Mathf.Abs(dist) < Mathf.Abs(minDistSame))
                {
                    minDistSame = dist;
                    nearestSameLane = info;
                }
            }

            return nearestSameLane != "none"
                ? nearestSameLane
                : $"no same-lane threat, nearest any: {nearestAnyLane}";
        }

        // ──────── Verbose logging ────────

        private void LogSnapshot(BotSceneSnapshot s)
        {
            BotLogger.Log(BotLogLevel.Verbose,
                $"[SNAPSHOT] hamster=(lane={(s.HamsterOnBottom ? "bottom" : "top")} energy={s.Energy} lives={s.Lives}) visible={s.VisibleObjects.Count}\n" +
                $"  objects: {BotLogger.FormatSnapshotObstacles(s.VisibleObjects)}");
        }

        private void LogClassify(BotSceneSnapshot s)
        {
            if (BotLogger.Level != BotLogLevel.Verbose) return;
            for (int i = 0; i < s.VisibleObjects.Count; i++)
            {
                var obs = s.VisibleObjects[i];
                BotLogger.Log(BotLogLevel.Verbose,
                    $"[CLASSIFY] #{i} {obs.Type} dist={obs.DistanceToHamster:F1} lane={(obs.IsTopLane ? "top" : "bottom")} → {obs.Category}");
            }
        }

        private void LogGenerate(List<ChainStep> steps)
        {
            if (BotLogger.Level != BotLogLevel.Verbose) return;
            foreach (var s in steps)
                BotLogger.Log(BotLogLevel.Verbose,
                    $"[GENERATE] {BotLogger.FormatStep(s)}");
        }

        private void LogNoSafeActions(BotSceneSnapshot snapshot)
        {
            string signature = BuildNoActionSignature(snapshot);
            bool unchanged = signature == _lastNoActionSignature;

            if (unchanged && Time.time - _lastNoActionLogTime < 1f)
            {
                _suppressedNoActionCount++;
                return;
            }

            string repeatedSuffix = unchanged && _suppressedNoActionCount > 0
                ? $" (unchanged x{_suppressedNoActionCount + 1})"
                : string.Empty;

            BotLogger.Log(BotLogLevel.Normal,
                $"[SELECT] no safe actions → None{repeatedSuffix}\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  visible: {BotLogger.FormatSnapshotObstacles(snapshot.VisibleObjects)}");

            _lastNoActionSignature = signature;
            _lastNoActionLogTime = Time.time;
            _suppressedNoActionCount = 0;
        }

        private static string BuildNoActionSignature(BotSceneSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append(snapshot.HamsterOnBottom ? 'B' : 'T');

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                builder.Append('|');
                builder.Append(obstacle.StableId);
                builder.Append(':');
                builder.Append(obstacle.Type);
                builder.Append(':');
                builder.Append(obstacle.Category);
                builder.Append(':');
                builder.Append(obstacle.IsTopLane ? 'T' : 'B');
            }

            return builder.ToString();
        }

        private void ResetNoActionLogState()
        {
            _lastNoActionSignature = null;
            _suppressedNoActionCount = 0;
            _lastNoActionLogTime = -999f;
        }
    }
}
