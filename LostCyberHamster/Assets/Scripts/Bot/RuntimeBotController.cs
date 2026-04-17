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
        private const float _initRetryInterval = 0.5f;
        private const string _hostObjectName = "[Bot]";

        private readonly CommittedPlan _committedPlan = new CommittedPlan();
        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();
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
        /// Гарантирует, что после загрузки сцены в runtime существует ровно один контроллер бота.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include) != null)
                return;

            GameObject host = GameObject.Find(_hostObjectName);
            if (host == null)
                host = new GameObject(_hostObjectName);

            host.AddComponent<RuntimeBotController>();
        }

        /// <summary>
        /// Переключает бота между включённым и выключенным состояниями.
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
        /// Выполняет один кадр цикла бота, когда runtime-зависимости готовы и игровой ран активен.
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
        /// Выполняет шаг бота: обновляет восприятие, продвигает текущий план и при необходимости активирует новый.
        /// </summary>
        private void TickBot()
        {
            if (!TryCaptureSnapshot())
                return;

            AdvanceCurrentPlan();
            if (_executor.IsActionInProgress)
                return;

            TrySetNewPlan();
        }

        /// <summary>
        /// Держит контроллер в ожидании, пока не найдены scene-зависимости и пока gameplay не перейдёт в тикаемое состояние.
        /// </summary>
        private bool IsReadyForTick()
        {
            if (!IsInitialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryResolveRuntimeDependencies();
                    _nextInitRetryTime = Time.time + _initRetryInterval;
                }

                return false;
            }

            return _gameManager.State == GameState.PLAYING
                && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
        }

        private bool TryCaptureSnapshot()
        {
            LastSnapshot = _snapshotBuilder.Build(_hamster);
            return LastSnapshot != null;
        }

        private void AdvanceCurrentPlan()
        {
            _executor.Tick(_hamster);
            _committedPlan.Replace(_executor.CurrentPlan);
        }

        private void TrySetNewPlan()
        {
            BotPlan plan = _planBuilder.Build(LastSnapshot, _committedPlan);
            if (!plan.HasActions || plan.IsEquivalentTo(_executor.CurrentPlan))
                return;

            _committedPlan.Replace(plan);
            _executor.SetPlan(plan);
            LogPlanActivation(plan);
        }

        /// <summary>
        /// Пишет краткую диагностическую строку для только что активированного плана.
        /// </summary>
        private static void LogPlanActivation(BotPlan plan)
        {
            DebugManager.DiagLog(
                $"[BotV2 PLAN] actions={plan.Actions.Count} " +
                $"score={plan.Score:F2} boundaryX={plan.CommittedBoundaryX:F2} " +
                $"head={plan.Actions[0].Description}");
        }

        /// <summary>
        /// Находит scene-зависимости контроллера и лениво подключает трекер runtime-событий.
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
