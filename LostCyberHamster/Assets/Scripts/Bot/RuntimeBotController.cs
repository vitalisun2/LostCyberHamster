using Assets.Scripts.Bot.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    public sealed class RuntimeBotController : MonoBehaviour
    {
        private const float InitRetryInterval = 0.5f;
        private const string HostObjectName = "[Bot]";

        private readonly CommittedPlan _committedPlan = new CommittedPlan();
        private readonly VisibilitySnapshotBuilder _snapshotBuilder = new VisibilitySnapshotBuilder();
        private readonly PlanExecutor _executor = new PlanExecutor();
        private readonly BotPlanRenderer _planRenderer = new BotPlanRenderer();

        private Hamster _hamster;
        private GameManager _gameManager;
        private PlanBuilder _planBuilder;
        private RuntimeBotEventTracker _eventTracker;
        private float _nextInitRetryTime;

        public bool IsEnabled { get; private set; } = true;
        public bool IsInitialized => _hamster != null && _gameManager != null;
        public BotPerceptionSnapshot LastSnapshot { get; private set; }
        public BotPlan CurrentPlan => _executor.CurrentPlan;

        /// <summary>
        /// Ensures that a single runtime controller instance exists after scene load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include) != null)
                return;

            GameObject host = GameObject.Find(HostObjectName);
            if (host == null)
                host = new GameObject(HostObjectName);

            host.AddComponent<RuntimeBotController>();
        }

        /// <summary>
        /// Switches the runtime bot between enabled and disabled states.
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

            _planBuilder = new PlanBuilder(
                new Assets.Scripts.Bot.Planning.ActionGenerator(),
                new Assets.Scripts.Bot.Planning.TransitionSimulator(),
                new Assets.Scripts.Bot.Planning.PlanEvaluator());
        }

        /// <summary>
        /// Runs one frame of the bot loop when runtime dependencies are ready and gameplay is active.
        /// </summary>
        private void Update()
        {
            if (!IsEnabled)
                return;

            if (!IsReadyForTick())
                return;

            TickBot();
        }

        private void OnRenderObject()
        {
            if (!IsInitialized || LastSnapshot == null || !CurrentPlan.HasActions)
                return;

            Camera camera = Camera.current;
            if (camera == null || camera != Camera.main)
                return;

            _planRenderer.Render(
                CurrentPlan,
                LastSnapshot,
                _hamster.IsOnBottomLine.Value,
                _executor.IsActionInProgress,
                camera);
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
            _planRenderer.Dispose();
        }

        private void Enable()
        {
            IsEnabled = true;
            if (!IsInitialized)
                TryResolveRuntimeDependencies();

            DebugManager.DiagLog("[BotV2] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            LastSnapshot = null;
            _committedPlan.Clear();
            _executor.Clear();
            DebugManager.DiagLog("[BotV2] Disabled");
        }

        /// <summary>
        /// Refreshes perception, advances the current plan, and replaces it only when a better pending plan is needed.
        /// </summary>
        private void TickBot()
        {
            LastSnapshot = _snapshotBuilder.Build(_hamster);
            if (LastSnapshot == null)
                return;

            _executor.Tick(_hamster);
            _committedPlan.Replace(_executor.CurrentPlan);

            if (_executor.IsActionInProgress)
                return;

            BotPlan plan = _planBuilder.Build(LastSnapshot, _committedPlan);
            if (!plan.HasActions || plan.IsEquivalentTo(_executor.CurrentPlan))
                return;

            _committedPlan.Replace(plan);
            _executor.SetPlan(plan);
            LogPlanActivation(plan);
        }

        /// <summary>
        /// Keeps the controller idle until scene references are resolved and gameplay is in a tickable state.
        /// </summary>
        private bool IsReadyForTick()
        {
            if (!IsInitialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryResolveRuntimeDependencies();
                    _nextInitRetryTime = Time.time + InitRetryInterval;
                }

                return false;
            }

            return _gameManager.State == GameState.PLAYING
                && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
        }

        /// <summary>
        /// Writes a concise diagnostic line for the newly activated plan.
        /// </summary>
        private static void LogPlanActivation(BotPlan plan)
        {
            DebugManager.DiagLog(
                $"[BotV2 PLAN] actions={plan.Actions.Count} " +
                $"score={plan.Score:F2} boundaryX={plan.CommittedBoundaryX:F2} " +
                $"head={plan.Actions[0].Description}");
        }

        /// <summary>
        /// Resolves the scene dependencies needed by the controller and lazily attaches the event tracker.
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
    }
}
