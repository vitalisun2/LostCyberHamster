using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Оркестратор Bot. Вешается на GameObject в сцене.
    /// Executor тикает каждый кадр, planner запускается только по runtime-триггерам.
    /// Горячая клавиша F1: вкл/выкл.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
        private const float InitRetryInterval = 0.5f;

        [Header("Visual Debug")]
        [SerializeField]
        [Tooltip("Показывать всю выбранную ветку Bot стрелками в Game view")]
        private bool _showPlannedTrajectory = true;

        public bool IsEnabled { get; private set; }
        public Hamster Hamster { get; private set; }
        public GameManager GameManager { get; private set; }
        public bool Initialized { get; private set; }
        public BotSceneSnapshot LastSnapshot { get; private set; }
        public CurrentPlan Plan { get; } = new CurrentPlan();

        private BotHud _hud;
        private readonly BotBranchRenderer _branchRenderer = new BotBranchRenderer();
        private readonly VisibleObjectAppearanceTracker _visibleObjectTracker = new VisibleObjectAppearanceTracker();
        private GameEventTracker _eventTracker;

        // Pipeline
        private SnapshotBuilder _snapshotBuilder;
        private ObjectClassifier _classifier;
        private BranchSelector _planner;
        private StepExecutor _executor;
        private BotPlanRuntime _planRuntime;

        private float _nextInitRetryTime;
        private bool _planEvaluationRequested = true;
        private bool _stepCompletedPending;

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

            // Попытка инициализации, если ещё не готов
            if (!Initialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryInit();
                    _nextInitRetryTime = Time.time + InitRetryInterval;
                }
                return;
            }

            // Работать только в активной игре с живым хомяком
            if (GameManager == null || GameManager.State != GameState.PLAYING)
                return;

            if (Hamster.HamsterState.Value == HamsterStateEnum.Dead)
                return;

            TickRuntime();
        }

        private void RequestPlanEvaluation() => _planEvaluationRequested = true;
        private void HandleStepCompleted() => _stepCompletedPending = true;

        /// <summary>
        /// Один тик runtime: обновить snapshot, продвинуть committed plan, при необходимости оценить замену и запустить head.
        /// </summary>
        private void TickRuntime()
        {
            BotSceneSnapshot liveSnapshot = RefreshSceneState();

            _executor.PollCompletion();

            if (_stepCompletedPending)
                AdvanceCommittedPlan(liveSnapshot);

            if (_executor.IsStepInProgress)
                return;

            if (_planEvaluationRequested)
                EvaluatePlan(liveSnapshot);

            _executor.TryFire();
        }

        private BotSceneSnapshot RefreshSceneState()
        {
            BotSceneSnapshot liveSnapshot = _snapshotBuilder.Build(Hamster);
            _visibleObjectTracker.Update(liveSnapshot);
            return liveSnapshot;
        }

        /// <summary>
        /// Продвигает committed plan после завершения текущего head.
        /// </summary>
        private void AdvanceCommittedPlan(BotSceneSnapshot liveSnapshot)
        {
            _stepCompletedPending = false;

            var head = _planRuntime.AdvancePlan(
                liveSnapshot,
                Hamster != null && Hamster.IsOnBottomLine.Value);

            if (head != null)
            {
                _executor.SetStep(head);
                return;
            }

            _executor.ClearStep();
            _planEvaluationRequested = true;
        }

        /// <summary>
        /// Оценивает замену текущего плана или строит новый, если committed plan пуст.
        /// </summary>
        private void EvaluatePlan(BotSceneSnapshot liveSnapshot)
        {
            LastSnapshot = liveSnapshot;

            var head = _planRuntime.CommitPlan(
                liveSnapshot,
                _planner.FindBestBranch(
                    liveSnapshot,
                    _classifier,
                    _planRuntime.SnapshotRetainableSteps()),
                Hamster != null && Hamster.IsOnBottomLine.Value);

            if (head != null)
                _executor.SetStep(head);
            else
                _executor.ClearStep();

            _planEvaluationRequested = false;
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
            DebugManager.DiagLog("[Bot] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            _planRuntime?.Clear();
            _executor?.ClearStep();
            _planRuntime?.ResetSelectionTracking();
            ResetRuntimeTracking();
            DebugManager.DiagLog("[Bot] Disabled");
        }

        /// <summary>
        /// Ищет зависимости в сцене (Hamster, GameManager) и создаёт pipeline.
        /// </summary>
        private void TryInit()
        {
            // Найти runtime-зависимости
            Hamster = FindAnyObjectByType<Hamster>();
            GameManager = FindAnyObjectByType<GameManager>();

            if (Hamster == null || GameManager == null)
                return;

            // Получить runtime shift из анимационных клипов
            var animController = Hamster.GetComponentInChildren<TransformAnimatorController>();
            float superJumpShift = HelpMethods.GetWorldShiftForClip(animController, BotConsts.SuperJumpClipName);
            float jumpOnRoofShift = HelpMethods.GetWorldShiftForClip(animController, BotConsts.JumpOnRoofClipName);

            // Создать pipeline и подписаться на события
            _eventTracker = new GameEventTracker(Hamster, GameManager);
            _snapshotBuilder = new SnapshotBuilder();
            _classifier = new ObjectClassifier();
            _planner = new BranchSelector(superJumpShift, jumpOnRoofShift);
            _executor = new StepExecutor(Hamster);
            _executor.OnStepCompleted += HandleStepCompleted;
            _visibleObjectTracker.OnNewObjectAppeared += RequestPlanEvaluation;
            _planRuntime = new BotPlanRuntime(Plan, _branchRenderer);
            ResetRuntimeTracking();

            Initialized = true;
            float worldWidth = Hamster.RightX - Hamster.LeftX;
            DebugManager.DiagLog(
                $"[Bot] Initialized | hamster LeftX={Hamster.LeftX:F2} RightX={Hamster.RightX:F2}" +
                $" worldWidth={worldWidth:F2} ColliderWidth(size.x)={Hamster.ColliderWidth:F2}");
        }

        private void ResetRuntimeTracking()
        {
            LastSnapshot = null;
            _planEvaluationRequested = true;
            _stepCompletedPending = false;
            _visibleObjectTracker.Reset();
        }

        private void OnDestroy()
        {
            _visibleObjectTracker.OnNewObjectAppeared -= RequestPlanEvaluation;
            if (_executor != null)
                _executor.OnStepCompleted -= HandleStepCompleted;
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
