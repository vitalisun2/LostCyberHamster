using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Оркестратор BotV3. Вешается на GameObject в сцене.
    /// Pipeline: Snapshot → Classify → Execute → Plan.
    /// Горячая клавиша F1: вкл/выкл.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
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
        private GameEventTracker _eventTracker;

        // Pipeline
        private SnapshotBuilder _snapshotBuilder;
        private ObjectClassifier _classifier;
        private BranchSelector _planner;
        private StepExecutor _executor;

        private const float InitRetryInterval = 0.5f;
        private float _nextInitRetryTime;

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

            RunPipeline();
        }

        private void RunPipeline()
        {
            // 1. Perceive
            LastSnapshot = _snapshotBuilder.Build(Hamster);
            _classifier.Classify(LastSnapshot);

            // 2. Если шаг уже в процессе, сначала даём executor'у шанс завершить его.
            if (_executor.IsStepInProgress)
            {
                _executor.TryExecute();

                if (_executor.IsStepInProgress && !_executor.WasCancelled)
                    return;
            }

            // 3. Для шага в Ready сначала перепланируем по live snapshot, затем исполняем head.
            Plan.RemoveCompletedFromHead();
            ApplyPlan(LastSnapshot, _planner.FindBestBranch(LastSnapshot, _classifier));
            _executor.TryExecute();
        }

        private void ApplyPlan(BotSceneSnapshot snapshot, BranchCandidate best)
        {
            if (best == null || best.Steps == null || best.Steps.Count == 0)
            {
                Plan.Clear();
                _executor.ClearStep();
                _branchRenderer.ClearPreview();
                return;
            }

            Plan.ReplaceFrom(best, best.Steps[0].Reason);
            _branchRenderer.UpdatePreview(
                Plan.Steps,
                snapshot != null ? snapshot.HamsterOnBottom : Hamster != null && Hamster.IsOnBottomLine.Value);

            if (Plan.Head != null)
                _executor.SetStep(Plan.Head);
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
            if (!Initialized)
                TryInit();
            DebugManager.DiagLog("[BotV3] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            Plan.Clear();
            _branchRenderer.ClearPreview();
            _executor?.ClearStep();
            _planner?.Reset();
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
            _executor = new StepExecutor(Hamster);

            Initialized = true;
            float worldWidth = Hamster.RightX - Hamster.LeftX;
            DebugManager.DiagLog(
                $"[BotV3] Initialized | hamster LeftX={Hamster.LeftX:F2} RightX={Hamster.RightX:F2}" +
                $" worldWidth={worldWidth:F2} ColliderWidth(size.x)={Hamster.ColliderWidth:F2}");
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
            _branchRenderer.Dispose();
        }

        private void OnGUI()
        {
            _hud?.Draw();
        }

        private void OnRenderObject()
        {
            if (!_showPlannedTrajectory || !IsEnabled || !_branchRenderer.HasPreview)
                return;

            Camera mainCamera = Camera.main;
            if (mainCamera == null || Camera.current != mainCamera)
                return;

            _branchRenderer.Render(mainCamera);
        }
    }
}
