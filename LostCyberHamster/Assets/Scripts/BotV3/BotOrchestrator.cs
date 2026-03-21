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
        public bool IsEnabled { get; private set; }
        public Hamster Hamster { get; private set; }
        public GameManager GameManager { get; private set; }
        public bool Initialized { get; private set; }
        public BotSceneSnapshot LastSnapshot { get; private set; }
        public CurrentPlan Plan { get; } = new CurrentPlan();

        private BotHud _hud;
        private GameEventTracker _eventTracker;

        // Pipeline
        private SnapshotBuilder _snapshotBuilder;
        private ObjectClassifier _classifier;
        private BotPlanner _planner;
        private StepExecutor _executor;

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
                    _nextInitRetryTime = Time.time + 0.5f;
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

            // 2. Execute active step (if any)
            _executor.TryExecute();

            // 3. Plan — replan when no active step or step was cancelled
            if (!_executor.HasActiveStep || _executor.WasCancelled)
            {
                Plan.RemoveCompletedFromHead();
                ApplyPlan(_planner.Replan(LastSnapshot, _classifier));
            }
        }

        private void ApplyPlan(ChainCandidate best)
        {
            if (best == null)
            {
                Plan.Clear();
                _executor.ClearStep();
                return;
            }

            Plan.ReplaceFrom(best, "SwitchLane threat avoidance");

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
            _planner = new BotPlanner();
            _executor = new StepExecutor(Hamster);

            Initialized = true;
            DebugManager.DiagLog("[BotV3] Initialized");
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
        }

        private void OnGUI()
        {
            _hud?.Draw();
        }
    }
}
