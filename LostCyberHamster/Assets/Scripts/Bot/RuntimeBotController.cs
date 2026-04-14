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

        private readonly CommittedPlan _committedPlan = new CommittedPlan();
        private readonly VisibilitySnapshotBuilder _snapshotBuilder = new VisibilitySnapshotBuilder();
        private readonly PlanExecutor _executor = new PlanExecutor();
        private readonly BotPlanRenderer _planRenderer = new BotPlanRenderer();

        private Hamster _hamster;
        private GameManager _gameManager;
        private PlanBuilder _planBuilder;
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

            GameObject host = GameObject.Find("[Bot V2]");
            if (host == null)
                host = new GameObject("[Bot V2]");

            host.AddComponent<RuntimeBotController>();
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

            _planRenderer.Render(CurrentPlan, LastSnapshot.RuntimeState.IsOnBottomLine, camera);
        }

        private void OnDestroy()
        {
            _planRenderer.Dispose();
        }

        private void TickBot()
        {
            LastSnapshot = _snapshotBuilder.Build(_hamster);
            if (LastSnapshot == null)
                return;

            _executor.Tick(_hamster);
            _committedPlan.Replace(_executor.CurrentPlan);

            if (_executor.IsActionInProgress || _executor.HasPendingActions)
                return;

            BotPlan plan = _planBuilder.Build(LastSnapshot, _committedPlan);
            if (!plan.HasActions)
                return;

            _committedPlan.Replace(plan);
            _executor.SetPlan(plan);
            Debug.Log($"[BotV2] Planned {plan.Actions.Count} action(s) on snapshot at {plan.CommittedBoundaryX:F2}.");
        }

        private void TryInit()
        {
            _hamster = FindAnyObjectByType<Hamster>(FindObjectsInactive.Exclude);
            _gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Exclude);
        }
    }
}
