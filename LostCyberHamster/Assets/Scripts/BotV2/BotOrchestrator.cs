using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using Sirenix.OdinInspector;
using System.Text;
using UnityEngine;
using Vues.GameCore;
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
        private float _scanRange = 20f;

        [SerializeField]
        public BotLogLevel LogLevel = BotLogLevel.Normal;

        [Title("Visual Debug")]
        [SerializeField]
        [Tooltip("Показывать траекторию выбранного one-step/two-step плана в Game view")]
        private bool _showPlannedTrajectory = true;

        [SerializeField]
        [Tooltip("Принудительно включает визуализацию траектории в play mode")]
        private bool _forceTrajectoryInPlayMode = true;

        [SerializeField]
        [Tooltip("Показывать текстовый HUD с текущим планом")]
        private bool _showTrajectoryHud = true;

        [SerializeField]
        private Color _trajectoryColor = new Color(0.25f, 1f, 0.25f, 0.95f);

        [SerializeField, Range(0.01f, 0.25f)]
        private float _trajectoryLineWidth = 0.11f;

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
        private ChainGenerator   _chainGenerator;
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

        private int _runStartCoins;
        private int _runStartCrystals;
        private int _runStartEnergy;
        private int _runStartLives;
        private int _coinsCollectedDuringRun;
        private int _coinsSpentDuringRun;
        private int _crystalsCollectedDuringRun;
        private int _crystalsSpentDuringRun;
        private int _energyAddedDuringRun;
        private int _energySpentDuringRun;
        private int _livesAddedDuringRun;
        private int _livesLostDuringRun;
        private int _actionsFiredDuringRun;
        private int _actionsCompletedDuringRun;
        private int _actionsCancelledDuringRun;
        private int _energySpentDuringBotActions;
        private float _lastActionFiredAt = -999f;
        private string _lastActionFiredLabel = "None";
        private Material _glMaterial;
        private ChainStep _previewFirstStep;
        private ChainStep _previewSecondStep;
        private bool _previewStep1LaneAfter;
        private string _trajectoryHudText = "Plan: none";
        private GUIStyle _trajectoryHudStyle;

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
            if (_forceTrajectoryInPlayMode)
                _showPlannedTrajectory = true;
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

            TrackUnattributedEnergyAndLives();

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
            {
                UpdateTrajectoryRenderer();
                return;
            }

            RunPipeline(snapshot, trigger);
            UpdateTrajectoryRenderer();
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

            ChainStep oneStepBest = _selector.Select(candidates);
            ChainStep bestChainFirst = null;
            ChainCandidate bestChain = null;
            var chainCandidates = _chainGenerator.Generate(snapshot, candidates, _classifier, _generator, _selector);
            if (chainCandidates.Count > 0)
            {
                bestChain = chainCandidates[0];
                bestChainFirst = bestChain.FirstStep;
                BotLogger.Log(BotLogLevel.Normal,
                    $"[CHAIN] selected two-step: first={bestChain.FirstStep.Action}/{bestChain.FirstStep.TargetObstacle.Type} " +
                    $"second={bestChain.SecondStep.Action}/{bestChain.SecondStep.TargetObstacle.Type} totalEnergy={bestChain.TotalEnergyCost}");
            }

            ChainStep best = SelectBestPlannedStep(oneStepBest, bestChain);
            if (bestChainFirst != null && best != bestChainFirst)
            {
                BotLogger.Log(BotLogLevel.Normal,
                    $"[CHAIN] fallback to better one-step: action={oneStepBest.Action} " +
                    $"cost={oneStepBest.EnergyCost} reason=\"{oneStepBest.Reason}\"");
            }

            if (best == null)
            {
                _lastAction = "None";
                ClearTrajectoryPreview();
                LogNoSafeActions(snapshot);
                return;
            }

            if (bestChain != null && best == bestChainFirst)
                UpdateTrajectoryPreview(snapshot, bestChain.FirstStep, bestChain.SecondStep);
            else
                UpdateTrajectoryPreview(snapshot, best, null);

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

        private static ChainStep SelectBestPlannedStep(ChainStep oneStepBest, ChainCandidate bestChain)
        {
            ChainStep chainFirst = bestChain?.FirstStep;

            if (oneStepBest == null)
                return chainFirst;
            if (chainFirst == null)
                return oneStepBest;

            // Stage 10: threat-сценарии предпочитают валидную двухшаговую цепочку,
            // чтобы не падать в ловушечный one-step fallback.
            bool oneStepThreat = oneStepBest.Rank == DecisionRank.ThreatSafety;
            bool chainThreat = bestChain.BestRank == DecisionRank.ThreatSafety;
            if (oneStepThreat && chainThreat)
                return chainFirst;

            // Stage 11: ранжирование по цепочке целиком.
            if (bestChain.BestRank != oneStepBest.Rank)
                return bestChain.BestRank.CompareTo(oneStepBest.Rank) < 0 ? chainFirst : oneStepBest;

            if (bestChain.TotalProfitScore != oneStepBest.ProfitScore)
                return bestChain.TotalProfitScore > oneStepBest.ProfitScore ? chainFirst : oneStepBest;

            if (bestChain.TotalEnergyCost != oneStepBest.EnergyCost)
                return bestChain.TotalEnergyCost < oneStepBest.EnergyCost ? chainFirst : oneStepBest;

            int compare = CompareStepPriority(chainFirst, oneStepBest);
            return compare >= 0 ? chainFirst : oneStepBest;
        }

        // >0 означает, что a лучше b; <0 — хуже; 0 — эквивалентны.
        private static int CompareStepPriority(ChainStep a, ChainStep b)
        {
            if (a.Rank != b.Rank)
                return b.Rank.CompareTo(a.Rank);

            if (a.ProfitScore != b.ProfitScore)
                return a.ProfitScore.CompareTo(b.ProfitScore);

            if (a.EnergyCost != b.EnergyCost)
                return b.EnergyCost.CompareTo(a.EnergyCost);

            return a.ExecuteAtDistance.CompareTo(b.ExecuteAtDistance);
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
                    DebugManager.DiagStability("[BotOrchestrator] Init failed — Hamster or GameManager not found");
                    _nextInitFailureLogTime = Time.time + 2f;
                }
                return;
            }

            _snapshotBuilder = new SnapshotBuilder();
            _classifier      = new ObjectClassifier();
            _generator       = new ActionGenerator();
            _selector        = new ActionSelector();
            _chainGenerator  = new ChainGenerator();
            _executor        = new StepExecutor(_hamster);

            if (_scanRange < 20f)
                _scanRange = 20f;

            ResetEconomyTracking();

            // Логирование урона
            _hamster.DamageEvent.Subscribe(OnDamage);
            _gameManager.OnFinish += OnGameFinished;
            GameEventsManager.OnLevelCompleted += OnLevelCompleted;
            GameEventsManager.OnCoinCollected += OnCoinCollected;
            GameEventsManager.OnCoinsSpent += OnCoinsSpent;
            GameEventsManager.OnCrystalsCollected += OnCrystalsCollected;
            GameEventsManager.OnCrystalsSpent += OnCrystalsSpent;
            GameEventsManager.OnEnergyAdded += OnEnergyAdded;
            GameEventsManager.OnEnergySpent += OnEnergySpent;
            GameEventsManager.OnLivesAdded += OnLivesAdded;
            GameEventsManager.OnLivesLost += OnLivesLost;

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
            GameEventsManager.OnCoinCollected -= OnCoinCollected;
            GameEventsManager.OnCoinsSpent -= OnCoinsSpent;
            GameEventsManager.OnCrystalsCollected -= OnCrystalsCollected;
            GameEventsManager.OnCrystalsSpent -= OnCrystalsSpent;
            GameEventsManager.OnEnergyAdded -= OnEnergyAdded;
            GameEventsManager.OnEnergySpent -= OnEnergySpent;
            GameEventsManager.OnLivesAdded -= OnLivesAdded;
            GameEventsManager.OnLivesLost -= OnLivesLost;
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

            var statusBeforeExecute = _activeStep.Status;
            _executor.TryExecute();

            if (statusBeforeExecute == ChainStepStatus.Ready && _activeStep != null && _activeStep.Status == ChainStepStatus.InProgress)
            {
                RegisterActionFired(_activeStep);
            }

            if (_executor.WasCancelled)
            {
                _actionsCancelledDuringRun++;
                DebugManager.DiagEconomy(
                    $"[ACTION CANCELLED] action={_activeStep.Action} reason=live_safety_recheck " +
                    $"energy={_hamster.Energy.Value} lives={_hamster.Lives.Value}");
                RememberCancelledSwitchLane();
                CompleteActiveStep(PipelineTrigger.StepCancelled);
                return false;
            }

            if (_activeStep.Status == ChainStepStatus.Completed)
            {
                _actionsCompletedDuringRun++;
                DebugManager.DiagEconomy(
                    $"[ACTION COMPLETED] action={_activeStep.Action} energy={_hamster.Energy.Value} lives={_hamster.Lives.Value}");
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
            DebugManager.DiagStability(
                $"[DAMAGE] hamster={BotLogger.FormatHamster(_hamster)} killer={killerInfo} activeStep={BotLogger.FormatStep(step)}");

            if (_hamster.Lives.Value <= 0)
            {
                DebugManager.DiagLog(
                    $"[TEST RESULT] FAIL\n" +
                    $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                    $"  active step: {BotLogger.FormatStep(step)}\n" +
                    $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, step?.TargetObstacle.StableId ?? 0)}");
                DebugManager.DiagStability("[TEST RESULT] FAIL");
                LogEconomyResult("FAIL", levelId: null, stars: null);
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
            LogEconomyResult("FINISH", levelId: null, stars: null);
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
            DebugManager.DiagStability($"[TEST RESULT] WIN level={levelId} stars={stars}");
            LogEconomyResult("WIN", levelId, stars);
        }

        private void ResetEconomyTracking()
        {
            _runStartCoins = ResourceManager.GetCurrentBalance(ResourceType.Coins);
            _runStartCrystals = ResourceManager.GetCurrentBalance(ResourceType.Crystals);
            _runStartEnergy = _hamster != null ? _hamster.Energy.Value : 0;
            _runStartLives = _hamster != null ? _hamster.Lives.Value : 0;
            _coinsCollectedDuringRun = 0;
            _coinsSpentDuringRun = 0;
            _crystalsCollectedDuringRun = 0;
            _crystalsSpentDuringRun = 0;
            _energyAddedDuringRun = 0;
            _energySpentDuringRun = 0;
            _livesAddedDuringRun = 0;
            _livesLostDuringRun = 0;
            _actionsFiredDuringRun = 0;
            _actionsCompletedDuringRun = 0;
            _actionsCancelledDuringRun = 0;
            _energySpentDuringBotActions = 0;
            _lastActionFiredAt = -999f;
            _lastActionFiredLabel = "None";

            DebugManager.DiagEconomy(
                $"[RUN START] coins={_runStartCoins} crystals={_runStartCrystals} energy={_runStartEnergy} lives={_runStartLives}");
        }

        private void OnCoinCollected(int amount)
        {
            if (amount > 0)
            {
                _coinsCollectedDuringRun += amount;
                int after = ResourceManager.GetCurrentBalance(ResourceType.Coins);
                int before = after - amount;
                DebugManager.DiagEconomy(
                    $"[COINS +] amount={amount} before={before} after={after} context={GetRecentActionContext(0.75f)}");
            }
        }

        private void OnCoinsSpent(int amount)
        {
            if (amount > 0)
            {
                _coinsSpentDuringRun += amount;
                int after = ResourceManager.GetCurrentBalance(ResourceType.Coins);
                int before = after + amount;
                DebugManager.DiagEconomy(
                    $"[COINS -] amount={amount} before={before} after={after} context={GetRecentActionContext(0.75f)}");
            }
        }

        private void OnCrystalsCollected(int amount)
        {
            if (amount > 0)
            {
                _crystalsCollectedDuringRun += amount;
                int after = ResourceManager.GetCurrentBalance(ResourceType.Crystals);
                int before = after - amount;
                DebugManager.DiagEconomy(
                    $"[CRYSTALS +] amount={amount} before={before} after={after} context={GetRecentActionContext(0.75f)}");
            }
        }

        private void OnCrystalsSpent(int amount)
        {
            if (amount > 0)
            {
                _crystalsSpentDuringRun += amount;
                int after = ResourceManager.GetCurrentBalance(ResourceType.Crystals);
                int before = after + amount;
                DebugManager.DiagEconomy(
                    $"[CRYSTALS -] amount={amount} before={before} after={after} context={GetRecentActionContext(0.75f)}");
            }
        }

        private void OnEnergyAdded(int amount)
        {
            if (amount > 0)
            {
                _energyAddedDuringRun += amount;
                int after = _hamster.Energy.Value;
                int before = after - amount;
                DebugManager.DiagEconomy(
                    $"[ENERGY +] amount={amount} before={before} after={after} context={GetRecentActionContext(0.75f)}");
            }
        }

        private void OnEnergySpent(int amount)
        {
            if (amount > 0)
            {
                _energySpentDuringRun += amount;
                if (IsRecentAction(0.75f))
                    _energySpentDuringBotActions += amount;

                int after = _hamster.Energy.Value;
                int before = after + amount;
                DebugManager.DiagEconomy(
                    $"[ENERGY -] amount={amount} before={before} after={after} context={GetRecentActionContext(0.75f)}");
            }
        }

        private void OnLivesAdded(int amount)
        {
            if (amount > 0)
            {
                _livesAddedDuringRun += amount;
                int after = _hamster.Lives.Value;
                int before = after - amount;
                DebugManager.DiagEconomy(
                    $"[LIVES +] amount={amount} before={before} after={after} context={GetRecentActionContext(1.2f)}");
            }
        }

        private void OnLivesLost(int amount)
        {
            if (amount > 0)
            {
                _livesLostDuringRun += amount;
                int after = _hamster.Lives.Value;
                int before = after + amount;
                DebugManager.DiagEconomy(
                    $"[LIVES -] amount={amount} before={before} after={after} context={GetRecentActionContext(1.2f)}");
            }
        }

        private void RegisterActionFired(ChainStep step)
        {
            _actionsFiredDuringRun++;
            _lastActionFiredAt = Time.time;
            _lastActionFiredLabel = step.Action.ToString();

            DebugManager.DiagEconomy(
                $"[ACTION FIRE] action={step.Action} rank={step.Rank} expectedEnergyCost={step.EnergyCost} " +
                $"target={step.TargetObstacle.Type}/{step.TargetObstacle.Category} energy={_hamster.Energy.Value} lives={_hamster.Lives.Value}");
        }

        private bool IsRecentAction(float seconds)
        {
            return Time.time - _lastActionFiredAt <= seconds;
        }

        private string GetRecentActionContext(float seconds)
        {
            return IsRecentAction(seconds) ? _lastActionFiredLabel : "None";
        }

        private void TrackUnattributedEnergyAndLives()
        {
            if (_hamster == null)
                return;

            // Эти изменения могут происходить вне событий (например, пассивный regen энергии).
            int expectedEnergy = _runStartEnergy + _energyAddedDuringRun - _energySpentDuringRun;
            int actualEnergy = _hamster.Energy.Value;
            int energyDrift = actualEnergy - expectedEnergy;
            if (energyDrift != 0)
            {
                if (energyDrift > 0)
                    _energyAddedDuringRun += energyDrift;
                else
                    _energySpentDuringRun += -energyDrift;

                DebugManager.DiagEconomy(
                    $"[ENERGY DRIFT] delta={energyDrift:+#;-#;0} source=unattributed actual={actualEnergy} expected={expectedEnergy}");
            }

            int expectedLives = _runStartLives + _livesAddedDuringRun - _livesLostDuringRun;
            int actualLives = _hamster.Lives.Value;
            int livesDrift = actualLives - expectedLives;
            if (livesDrift != 0)
            {
                if (livesDrift > 0)
                    _livesAddedDuringRun += livesDrift;
                else
                    _livesLostDuringRun += -livesDrift;

                DebugManager.DiagEconomy(
                    $"[LIVES DRIFT] delta={livesDrift:+#;-#;0} source=unattributed actual={actualLives} expected={expectedLives}");
            }
        }

        private void LogEconomyResult(string result, int? levelId, int? stars)
        {
            string levelPart = levelId.HasValue ? $" level={levelId.Value}" : string.Empty;
            string starsPart = stars.HasValue ? $" stars={stars.Value}" : string.Empty;
            float economicScore = CalculateEconomicScore();

            DebugManager.DiagEconomy(
                $"[RUN RESULT] result={result}{levelPart}{starsPart}\n" +
                BuildEconomySummary() + "\n" +
                $"  actionStats: fired={_actionsFiredDuringRun} completed={_actionsCompletedDuringRun} cancelled={_actionsCancelledDuringRun}\n" +
                $"  score: economicScore={economicScore:F2} energySpentByBotActions={_energySpentDuringBotActions}");
        }

        private string BuildEconomySummary()
        {
            int endCoins = ResourceManager.GetCurrentBalance(ResourceType.Coins);
            int endCrystals = ResourceManager.GetCurrentBalance(ResourceType.Crystals);
            int endEnergy = _hamster != null ? _hamster.Energy.Value : 0;
            int endLives = _hamster != null ? _hamster.Lives.Value : 0;

            return $"  economy: " +
                   $"coins {FormatResourceSummary(_runStartCoins, endCoins, _coinsCollectedDuringRun, _coinsSpentDuringRun)}; " +
                   $"crystals {FormatResourceSummary(_runStartCrystals, endCrystals, _crystalsCollectedDuringRun, _crystalsSpentDuringRun)}; " +
                   $"energy {FormatResourceSummary(_runStartEnergy, endEnergy, _energyAddedDuringRun, _energySpentDuringRun)}; " +
                   $"lives {FormatResourceSummary(_runStartLives, endLives, _livesAddedDuringRun, _livesLostDuringRun)}";
        }

        private float CalculateEconomicScore()
        {
            int endCoins = ResourceManager.GetCurrentBalance(ResourceType.Coins);
            int endCrystals = ResourceManager.GetCurrentBalance(ResourceType.Crystals);
            int endEnergy = _hamster != null ? _hamster.Energy.Value : 0;
            int endLives = _hamster != null ? _hamster.Lives.Value : 0;

            int netCoins = endCoins - _runStartCoins;
            int netCrystals = endCrystals - _runStartCrystals;
            int netEnergy = endEnergy - _runStartEnergy;
            int netLives = endLives - _runStartLives;

            // Простая линейная сводка для сравнения прогонов между собой.
            return netCoins * 1.0f +
                   netCrystals * 2.0f +
                   netEnergy * 0.2f +
                   netLives * 8.0f -
                   _actionsCancelledDuringRun * 0.5f;
        }

        private static string FormatResourceSummary(int start, int end, int collected, int spent)
        {
            int net = end - start;
            return $"start={start} end={end} net={net:+#;-#;0} collected={collected} spent={spent}";
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

        private void UpdateTrajectoryPreview(BotSceneSnapshot snapshot, ChainStep first, ChainStep second)
        {
            _previewFirstStep = first;
            _previewSecondStep = second;

            if (!_showPlannedTrajectory || snapshot == null || first == null)
            {
                _trajectoryHudText = "Plan: none";
                return;
            }

            _previewStep1LaneAfter = GetLaneAfterStep(first, snapshot.HamsterOnBottom);

            _trajectoryHudText = second == null
                ? $"Plan: {first.Action} ({first.Rank})"
                : $"Plan: {first.Action} -> {second.Action} ({first.Rank}/{second.Rank})";
        }

        private static bool GetLaneAfterStep(ChainStep step, bool currentOnBottom)
        {
            if (step.Action != BotAction.SwitchLane)
                return currentOnBottom;
            if (step.TargetObstacle.Category == ObjectCategory.Threat)
                return step.TargetObstacle.IsTopLane;
            return !step.TargetObstacle.IsTopLane;
        }

        private void UpdateTrajectoryRenderer()
        {
            if (_showPlannedTrajectory)
                EnsureGLMaterial();
        }

        private void EnsureGLMaterial()
        {
            if (_glMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                DebugManager.DiagLog("[BotOrchestrator] Hidden/Internal-Colored shader not found!");
                return;
            }

            _glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _glMaterial.SetInt("_ZWrite", 0);
            _glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            DebugManager.DiagLog("[BotOrchestrator] Trajectory GL material created (ZTest=Always)");
        }

        private void OnRenderObject()
        {
            if (!_showPlannedTrajectory || !IsEnabled || _glMaterial == null)
                return;
            if (_previewFirstStep == null)
                return;

            Camera cam = Camera.main;
            if (cam == null) return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            bool hamBottom = _hamster != null && _hamster.IsOnBottomLine.Value;
            DrawStepIndicator(_previewFirstStep, hamBottom, stepAlpha: 1.0f);
            if (_previewSecondStep != null)
                DrawStepIndicator(_previewSecondStep, _previewStep1LaneAfter, stepAlpha: 0.6f);

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawStepIndicator(ChainStep step, bool hamOnBottom, float stepAlpha = 1.0f)
        {
            var spawner = ObstacleSpawner.Instance;
            float obsLeft   = step.TargetObstacle.LeftX;
            float obsRight  = step.TargetObstacle.RightX;
            float obsCenter = step.TargetObstacle.CenterX;

            if (spawner != null)
            {
                var spawned = spawner.SpawnedObstacles;
                for (int i = 0; i < spawned.Count; i++)
                {
                    var inst = spawned[i];
                    if (inst?.ObstacleScript == null) continue;
                    if (inst.ObstacleScript.GetInstanceID() != step.TargetObstacle.StableId) continue;
                    float hw = inst.ObstacleScript.ColliderWidth * 0.5f;
                    obsCenter = inst.ObstacleScript.transform.position.x;
                    obsLeft   = obsCenter - hw;
                    obsRight  = obsCenter + hw;
                    break;
                }
            }

            float laneY = hamOnBottom
                ? Assets.Scripts.Consts.ObstacleY1Pos + 0.95f
                : Assets.Scripts.Consts.ObstacleY0Pos + 0.95f;
            float roofY = hamOnBottom
                ? Assets.Scripts.Consts.ObstacleRoofY1Pos + 0.15f
                : Assets.Scripts.Consts.ObstacleRoofY0Pos + 0.15f;

            Color baseColor = GetStepColor(step.Action);
            GL.Color(new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * stepAlpha));

            bool isJumpOn = step.Action == BotAction.Jump &&
                            (step.TargetObstacle.Category == ObjectCategory.Target ||
                             step.TargetObstacle.Type == ObstacleTypeEnum.bigNotAlive ||
                             step.TargetObstacle.Type == ObstacleTypeEnum.mediumNotAlive);

            if (step.Action == BotAction.SuperJump)
            {
                // Single large arc — peak 2.62 from transform_super_jump.anim
                float startX = obsLeft - 0.3f;
                float endX   = obsRight + 2.0f;
                float midX   = (startX + obsRight) * 0.5f;
                DrawQuadBezierArc(
                    new Vector3(startX, laneY, 0f),
                    new Vector3(midX, laneY + 2.62f, 0f),
                    new Vector3(endX, laneY, 0f), 12);
                DrawArrowhead(new Vector3(endX, laneY, 0f), new Vector3(midX, laneY + 2.62f, 0f), 0.45f);
            }
            else if (isJumpOn)
            {
                // Two arcs (напрыгивание) — from transform_jump_on.anim:
                //   Arc1 peak = +1.42, valley = +0.80, Arc2 peak = +2.40, land at roofY
                float startX = obsLeft - 0.3f;
                float midX   = obsCenter;
                float valleyY = laneY + 0.80f;
                float endX   = obsRight - 0.2f;

                // Arc 1: ground → valley on top of obstacle front
                DrawQuadBezierArc(
                    new Vector3(startX, laneY, 0f),
                    new Vector3((startX + midX) * 0.5f, laneY + 1.42f, 0f),
                    new Vector3(midX, valleyY, 0f), 8);
                // Arc 2: valley → roof landing
                DrawQuadBezierArc(
                    new Vector3(midX, valleyY, 0f),
                    new Vector3((midX + endX) * 0.5f, laneY + 2.40f, 0f),
                    new Vector3(endX, roofY, 0f), 8);
                DrawArrowhead(
                    new Vector3(endX, roofY, 0f),
                    new Vector3((midX + endX) * 0.5f, laneY + 2.40f, 0f), 0.40f);
            }
            else if (step.Action == BotAction.Jump)
            {
                // Jump over small obstacle — peak 1.42 from transform_jump.anim
                float startX = obsLeft - 0.3f;
                float endX   = obsRight + 1.5f;
                float midX   = (startX + obsRight) * 0.5f;
                DrawQuadBezierArc(
                    new Vector3(startX, laneY, 0f),
                    new Vector3(midX, laneY + 1.42f, 0f),
                    new Vector3(endX, laneY, 0f), 10);
                DrawArrowhead(new Vector3(endX, laneY, 0f), new Vector3(midX, laneY + 1.42f, 0f), 0.40f);
            }
            else if (step.Action == BotAction.SwitchLane)
            {
                float topY    = Assets.Scripts.Consts.ObstacleY0Pos + 0.95f;
                float botY    = Assets.Scripts.Consts.ObstacleY1Pos + 0.95f;
                float targetY = hamOnBottom ? topY : botY;
                float xPos    = obsLeft - 0.2f;
                var from = new Vector3(xPos, laneY,   0f);
                var to   = new Vector3(xPos, targetY, 0f);
                GL.Vertex(from); GL.Vertex(to);
                Vector3 dir = (to - from).normalized;
                GL.Vertex(to); GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f,  150f) * dir) * 0.35f);
                GL.Vertex(to); GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * dir) * 0.35f);
                GL.Vertex(new Vector3(xPos - 0.25f, laneY,    0f)); GL.Vertex(new Vector3(xPos + 0.25f, laneY,    0f));
                GL.Vertex(new Vector3(xPos - 0.25f, targetY,  0f)); GL.Vertex(new Vector3(xPos + 0.25f, targetY,  0f));
            }
        }

        private static void DrawQuadBezierArc(Vector3 p0, Vector3 ctrl, Vector3 p2, int segments)
        {
            Vector3 prev = p0;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float u = 1f - t;
                Vector3 pt = u * u * p0 + 2f * u * t * ctrl + t * t * p2;
                GL.Vertex(prev);
                GL.Vertex(pt);
                prev = pt;
            }
        }

        private static void DrawArrowhead(Vector3 end, Vector3 approachCtrl, float size)
        {
            Vector3 dir = (end - approachCtrl).normalized;
            GL.Vertex(end); GL.Vertex(end + (Vector3)(Quaternion.Euler(0f, 0f,  150f) * dir) * size);
            GL.Vertex(end); GL.Vertex(end + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * dir) * size);
        }

        private static Color GetStepColor(BotAction action)
        {
            switch (action)
            {
                case BotAction.SwitchLane:
                    return new Color(0.25f, 0.95f, 1f, 1f);
                case BotAction.Jump:
                    return new Color(1f, 0.95f, 0.2f, 1f);
                case BotAction.SuperJump:
                    return new Color(1f, 0.55f, 0.2f, 1f);
                default:
                    return new Color(0.8f, 0.8f, 0.8f, 0.95f);
            }
        }

        private void ClearTrajectoryPreview()
        {
            _previewFirstStep = null;
            _previewSecondStep = null;
            _trajectoryHudText = "Plan: none";
        }

        private void OnGUI()
        {
            if (!_showTrajectoryHud || !IsEnabled)
                return;

            if (_trajectoryHudStyle == null)
            {
                _trajectoryHudStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }

            string text = _trajectoryHudText;
            if (_previewFirstStep != null)
            {
                string step1 = $"Step1: {_previewFirstStep.Action} ({_previewFirstStep.TargetObstacle.Category})";
                string step2 = _previewSecondStep != null
                    ? $"Step2: {_previewSecondStep.Action} ({_previewSecondStep.TargetObstacle.Category})"
                    : "Step2: none";
                text = $"{text}\n{step1}\n{step2}";
            }

            var rect = new Rect(20f, 20f, 760f, 100f);
            GUI.Box(rect, text, _trajectoryHudStyle);
        }
    }
}
