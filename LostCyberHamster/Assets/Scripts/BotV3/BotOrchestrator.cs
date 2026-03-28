using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Оркестратор BotV3. Вешается на GameObject в сцене.
    /// Executor тикает каждый кадр, planner запускается только по runtime-триггерам.
    /// Горячая клавиша F1: вкл/выкл.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
        private const float InitRetryInterval = 0.5f;

        [Header("Visual Debug")]
        [SerializeField]
        [Tooltip("Показывать всю выбранную ветку BotV3 стрелками в Game view")]
        private bool _showPlannedTrajectory = true;

        public bool IsEnabled { get; private set; }
        public Hamster Hamster { get; private set; }
        public GameManager GameManager { get; private set; }
        public bool Initialized { get; private set; }
        public BotSceneSnapshot LastSnapshot { get; private set; }
        public CurrentPlan Plan { get; } = new CurrentPlan();

        private BotHud _hud;
        private readonly BotBranchRenderer _branchRenderer = new BotBranchRenderer();
        private readonly VisibleObjectBaselineTracker _visibleObjectBaseline = new VisibleObjectBaselineTracker();
        private GameEventTracker _eventTracker;

        // Pipeline
        private SnapshotBuilder _snapshotBuilder;
        private ObjectClassifier _classifier;
        private BranchSelector _planner;
        private BotPlanRuntime _planRuntime;

        private float _nextInitRetryTime;
        private bool _replanRequested = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<BotOrchestrator>(FindObjectsInactive.Include) != null)
                return;

            var host = GameObject.Find("[Bot]");
            if (host == null)
                host = new GameObject("[Bot]");

            host.AddComponent<BotOrchestrator>();
        }

        private void Start()
        {
            _hud = new BotHud(this);
            Enable();
        }

        private void Update()
        {
            if (!IsEnabled)
                return;

            if (!Initialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryInit();
                    _nextInitRetryTime = Time.time + InitRetryInterval;
                }
                return;
            }

            if (GameManager == null || GameManager.State != GameState.PLAYING)
                return;

            if (Hamster.HamsterState.Value == HamsterStateEnum.Dead)
                return;

            TickRuntime();
        }

        private void TickRuntime()
        {
            BotSceneSnapshot liveSnapshot = _snapshotBuilder.Build(Hamster);
            if (_visibleObjectBaseline.Update(liveSnapshot))
                _replanRequested = true;

            if (_planRuntime.IsStepInProgress)
            {
                if (_planRuntime.TryExecute() == StepExecutionTickResult.StepCompleted)
                {
                    _planRuntime.RemoveCompletedFromHead();
                    _replanRequested = true;
                }

                if (_planRuntime.IsStepInProgress)
                    return;
            }

            if (_replanRequested)
                Replan(liveSnapshot);

            if (_planRuntime.TryExecute() == StepExecutionTickResult.StepCompleted)
            {
                _planRuntime.RemoveCompletedFromHead();
                _replanRequested = true;
            }
        }

        private void Replan(BotSceneSnapshot liveSnapshot)
        {
            var classifiedSnapshot = _classifier.Classify(liveSnapshot);
            LastSnapshot = classifiedSnapshot;
            _planRuntime.RemoveCompletedFromHead();
            _planRuntime.ApplyPlan(
                classifiedSnapshot,
                _planner.FindBestBranch(classifiedSnapshot, _classifier),
                Hamster != null && Hamster.IsOnBottomLine.Value);
            _replanRequested = false;
        }

        public void ToggleEnabledFromHotkey()
        {
            if (IsEnabled)
                Disable();
            else
                Enable();
        }

        private void Enable()
        {
            IsEnabled = true;
            ResetRuntimeTracking();
            if (!Initialized)
                TryInit();
            DebugManager.DiagLog("[BotV3] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            _planRuntime?.ClearRuntime();
            _planRuntime?.ResetSelectionTracking();
            ResetRuntimeTracking();
            DebugManager.DiagLog("[BotV3] Disabled");
        }

        private void TryInit()
        {
            Hamster = FindAnyObjectByType<Hamster>();
            GameManager = FindAnyObjectByType<GameManager>();

            if (Hamster == null || GameManager == null)
                return;

            _eventTracker = new GameEventTracker(Hamster, GameManager);
            _snapshotBuilder = new SnapshotBuilder();
            _classifier = new ObjectClassifier();
            _planner = new BranchSelector();
            _planRuntime = new BotPlanRuntime(Plan, new StepExecutor(Hamster), _branchRenderer);
            ResetRuntimeTracking();

            Initialized = true;
            float worldWidth = Hamster.RightX - Hamster.LeftX;
            DebugManager.DiagLog(
                $"[BotV3] Initialized | hamster LeftX={Hamster.LeftX:F2} RightX={Hamster.RightX:F2}" +
                $" worldWidth={worldWidth:F2} ColliderWidth(size.x)={Hamster.ColliderWidth:F2}");
        }

        private void ResetRuntimeTracking()
        {
            LastSnapshot = null;
            _replanRequested = true;
            _visibleObjectBaseline.Reset();
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
            _planRuntime?.Dispose();
            if (_planRuntime == null)
                _branchRenderer.Dispose();
        }

        private void OnGUI()
        {
            _hud?.Draw();
        }

        private void OnRenderObject()
        {
            if (!_showPlannedTrajectory || !IsEnabled || _planRuntime == null || !_planRuntime.HasPreview)
                return;

            Camera mainCamera = Camera.main;
            if (mainCamera == null || Camera.current != mainCamera)
                return;

            _planRuntime.Render(mainCamera);
        }
    }
}
