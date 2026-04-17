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
            DontDestroyOnLoad(gameObject);

            _planBuilder = CreatePlanBuilder();
        }

        private void Update()
        {
            if (!IsEnabled)
                return;

            if (ShouldWaitForInitialization())
                return;

            if (!CanTickGameplay())
                return;

            TickBot();
        }

        private void OnRenderObject()
        {
            if (!TryGetRenderCamera(out Camera camera))
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
            ResetBotState();
            DebugManager.DiagLog("[BotV2] Disabled");
        }

        private void TickBot()
        {
            if (!TryRefreshSnapshot())
                return;

            if (TryContinueCurrentAction())
                return;

            if (!TryBuildNextPlan(out BotPlan plan))
                return;

            ApplyPlanIfChanged(plan);
        }

        private static PlanBuilder CreatePlanBuilder()
        {
            return new PlanBuilder(
                new Assets.Scripts.Bot.Planning.ActionGenerator(),
                new Assets.Scripts.Bot.Planning.TransitionSimulator(),
                new Assets.Scripts.Bot.Planning.PlanEvaluator());
        }

        private bool ShouldWaitForInitialization()
        {
            if (IsInitialized)
                return false;

            TryInitializeOnRetry();
            return true;
        }

        private void TryInitializeOnRetry()
        {
            if (Time.time < _nextInitRetryTime)
                return;

            TryResolveRuntimeDependencies();
            _nextInitRetryTime = Time.time + InitRetryInterval;
        }

        private bool CanTickGameplay()
        {
            return _gameManager.State == GameState.PLAYING
                && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
        }

        private bool TryGetRenderCamera(out Camera camera)
        {
            camera = Camera.current;
            return IsInitialized
                && LastSnapshot != null
                && CurrentPlan.HasActions
                && camera != null
                && camera == Camera.main;
        }

        private void ResetBotState()
        {
            LastSnapshot = null;
            _committedPlan.Clear();
            _executor.Clear();
        }

        private bool TryRefreshSnapshot()
        {
            LastSnapshot = _snapshotBuilder.Build(_hamster);
            return LastSnapshot != null;
        }

        private bool TryContinueCurrentAction()
        {
            _executor.Tick(_hamster);
            _committedPlan.Replace(_executor.CurrentPlan);
            return _executor.IsActionInProgress;
        }

        private bool TryBuildNextPlan(out BotPlan plan)
        {
            plan = _planBuilder.Build(LastSnapshot, _committedPlan);
            return plan.HasActions;
        }

        private void ApplyPlanIfChanged(BotPlan plan)
        {
            if (plan.IsEquivalentTo(_executor.CurrentPlan))
                return;

            _committedPlan.Replace(plan);
            _executor.SetPlan(plan);
            LogPlanActivation(plan);
        }

        private static void LogPlanActivation(BotPlan plan)
        {
            DebugManager.DiagLog(
                $"[BotV2 PLAN] actions={plan.Actions.Count} " +
                $"score={plan.Score:F2} boundaryX={plan.CommittedBoundaryX:F2} " +
                $"head={plan.Actions[0].Description}");
        }

        private void TryResolveRuntimeDependencies()
        {
            ResolveSceneReferences();

            if (!IsInitialized)
                return;

            EnsureEventTracker();
        }

        private void ResolveSceneReferences()
        {
            _hamster = FindAnyObjectByType<Hamster>(FindObjectsInactive.Exclude);
            _gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Exclude);
        }

        private void EnsureEventTracker()
        {
            if (_eventTracker == null)
                _eventTracker = new RuntimeBotEventTracker(_hamster, _gameManager);
        }
    }
}
