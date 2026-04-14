using Assets.Scripts.Bot.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
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
            _planBuilder = new PlanBuilder(
                new Assets.Scripts.Bot.Planning.ActionGenerator(),
                new Assets.Scripts.Bot.Planning.TransitionSimulator(),
                new Assets.Scripts.Bot.Planning.PlanEvaluator());
        }

        private void Update()
        {
            if (!IsEnabled)
                return;

            if (!IsInitialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryInit();
                    _nextInitRetryTime = Time.time + InitRetryInterval;
                }

                return;
            }

            if (_gameManager.State != GameState.PLAYING)
                return;

            if (_hamster.HamsterState.Value == Gameplay.Enums.HamsterStateEnum.Dead)
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
                TryInit();

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
            if (!plan.HasActions)
                return;

            if (plan.IsEquivalentTo(_executor.CurrentPlan))
                return;

            _committedPlan.Replace(plan);
            _executor.SetPlan(plan);
            DebugManager.DiagLog(
                $"[BotV2 PLAN] actions={plan.Actions.Count} " +
                $"score={plan.Score:F2} boundaryX={plan.CommittedBoundaryX:F2} " +
                $"head={plan.Actions[0].Description}");
        }

        private void TryInit()
        {
            _hamster = FindAnyObjectByType<Hamster>(FindObjectsInactive.Exclude);
            _gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Exclude);

            if (_hamster == null || _gameManager == null)
                return;

            if (_eventTracker == null)
                _eventTracker = new RuntimeBotEventTracker(_hamster, _gameManager);
        }
    }
}
