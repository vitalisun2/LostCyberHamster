using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Главный оркестратор бота. Вешается на GameObject в сцене.
    /// Управляет хомяком через детерминированный Chain Planner.
    /// Горячая клавиша: F1 — вкл/выкл.
    /// </summary>
    public class HamsterBot : MonoBehaviour
    {
        public static HamsterBot Instance { get; private set; }

        // ──────────────── Inspector ────────────────

        [Title("HamsterBot Settings")]
        [SerializeField] private bool _enabledOnStart;

        [SerializeField, Range(5f, 30f)]
        [Tooltip("Дальность сканирования (мировые единицы)")]
        private float _scanRange = 15f;

        [SerializeField]
        [Tooltip("Авто-рестарт уровня при смерти хомяка")]
        private bool _autoRestartOnDeath;

        [SerializeField, Range(1f, 5f)]
        [Tooltip("Задержка перед авто-рестартом (сек)")]
        private float _autoRestartDelay = 2f;

        [Title("Runtime Info"), ReadOnly]
        [ShowInInspector] public bool IsEnabled { get; private set; }

        [ShowInInspector, ReadOnly]
        private string _lastDecisionText = "—";

        [ShowInInspector, ReadOnly]
        private int ActionsExecuted => _timingPolicy?.ActionsExecuted ?? 0;

        // ──────────────── Internals ────────────────

        private Hamster _hamster;
        private SnapshotBuilder _snapshotBuilder;
        private ObjectClassifier _classifier;
        private ChainGenerator _chainGenerator;
        private ChainScorer _chainScorer;
        private PlanValidator _planValidator;
        private PlanSelector _planSelector;
        private BotTimingPolicy _timingPolicy;
        private CurrentPlan _currentPlan = new CurrentPlan();
        private GameManager _gameManager;
        private bool _initialized;

        // Триггеры пересчёта
        private System.Collections.Generic.HashSet<int> _prevObjectIds = new System.Collections.Generic.HashSet<int>();
        private int _framesSinceRecalc;
        private const int MaxFramesWithoutRecalc = 10;

        // Watchdog: обнаружение застревания в неконтролируемом состоянии
        private float _uncontrollableStartTime;
        private float _lastWatchdogWarnTime;
        private bool _inUncontrollableState;
        private const float UncontrollableWarnTime = 2f;

        // QA-логирование
        private int _replanCount;

        // ──────────────── Lifecycle ────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (_enabledOnStart)
                TryInitAndEnable();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsEnabled && !_enabledOnStart) return;

            _initialized = false;
            _hamster = null;
            _currentPlan = new CurrentPlan();
            _prevObjectIds.Clear();

            StartCoroutine(ReinitAfterSceneLoad());
        }

        private IEnumerator ReinitAfterSceneLoad()
        {
            yield return new WaitForSeconds(0.5f);

            if (!IsEnabled) yield break;

            TryInitAndEnable();
            DebugManager.DiagLog("[HamsterBot] Re-initialized after scene load. Waiting for PLAYING state.");
        }

        private void Update()
        {
            if (!IsEnabled || !_initialized || _hamster == null) return;

            // Ждём, пока игра реально запустится (после интро)
            if (_gameManager == null || _gameManager.State != GameState.PLAYING)
                return;

            if (_hamster.HamsterState.Value == HamsterStateEnum.Dead)
            {
                HandleDeath();
                return;
            }

            // Watchdog: не действуем в процессе прыжка/смены линии
            var currentState = _hamster.HamsterState.Value;
            if (!IsControllableState(currentState))
            {
                if (!_inUncontrollableState)
                {
                    _inUncontrollableState = true;
                    _uncontrollableStartTime = Time.time;
                    _lastWatchdogWarnTime = Time.time;
                }
                else
                {
                    float elapsed = Time.time - _uncontrollableStartTime;
                    if (Time.time - _lastWatchdogWarnTime >= UncontrollableWarnTime)
                    {
                        _lastWatchdogWarnTime = Time.time;
                        DebugManager.DiagLog($"[HamsterBot] WARNING: stuck in state={currentState} for {elapsed:F1}s");
                    }
                }
                return;
            }

            if (_inUncontrollableState)
            {
                _inUncontrollableState = false;
                DebugManager.DiagLog($"[HamsterBot] Returned to controllable state={currentState} after {Time.time - _uncontrollableStartTime:F1}s");
            }

            // ── 1. Удалить завершённые шаги из головы плана ──
            _currentPlan.RemoveCompletedFromHead();

            // ── 2. Исполнение текущего шага (включая InProgress→Completed) ──
            _timingPolicy.TryExecuteHead(_currentPlan);

            // ── 3. Строим snapshot для проверки триггеров ──
            var snapshot = _snapshotBuilder.Build(_hamster, _scanRange);

            if (!NeedsReplan(snapshot)) return;

            // ── 4. Pipeline пересчёта ──
            _classifier.Classify(snapshot);
            var decision = _planValidator.Validate(snapshot, _currentPlan);

            System.Collections.Generic.List<ChainCandidate> candidates;
            if (decision == PlanDecision.KeepTail)
            {
                var tailState = GetTailProjectedState(snapshot);
                candidates = _chainGenerator.Generate(snapshot.VisibleObjects, tailState);
            }
            else
            {
                var initialState = ProjectedState.FromSnapshot(snapshot);
                candidates = _chainGenerator.Generate(snapshot.VisibleObjects, initialState);
            }

            _chainScorer.Score(candidates);
            _currentPlan = _planSelector.Select(decision, _currentPlan, candidates);
            _lastDecisionText = _currentPlan?.Strategy ?? "—";
            _framesSinceRecalc = 0;

            DebugManager.DiagLog(BuildReplanLog(decision, candidates));
        }

        private string BuildReplanLog(
            PlanDecision decision,
            System.Collections.Generic.List<ChainCandidate> candidates)
        {
            _replanCount++;

            int safeCount = 0;
            float bestScore = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].AllStepsSafe) safeCount++;
                if (candidates[i].Score > bestScore) bestScore = candidates[i].Score;
            }

            // Список действий: SwitchLane→Jump→...
            var sb = new System.Text.StringBuilder();
            var steps = _currentPlan?.Steps;
            if (steps != null && steps.Count > 0)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (i > 0) sb.Append('→');
                    sb.Append(steps[i].Action);
                }
            }
            else sb.Append("(empty)");
            string actionList = sb.ToString();

            // Нумерованный список шагов с дистанциями
            sb.Clear();
            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var s = steps[i];
                    string target = s.TargetObstacle.HasValue
                        ? s.TargetObstacle.Value.Type.ToString()
                        : "—";
                    sb.Append($"[{i + 1}]{s.Action}({target})@d={s.ExecuteAtDistance:F1} ");
                }
            }
            string planDetail = sb.ToString().TrimEnd();

            int cost = _currentPlan?.Steps.Count > 0
                ? _currentPlan.Steps[0].EnergyCost
                : 0;
            int targets = 0;
            if (candidates.Count > 0) targets = candidates[0].TargetsDestroyed;

            return $"[Bot#{_replanCount}] REPLAN: {decision}\n" +
                   $"  Candidates: {candidates.Count} generated, {safeCount} safe, best score={bestScore:F2}\n" +
                   $"  Selected: [{actionList}] cost={cost} benefit={targets}target(s)\n" +
                   $"  Plan: {planDetail}";
        }

        // ──────────────── Replan триггеры ────────────────

        private bool NeedsReplan(BotSceneSnapshot snapshot)
        {
            // Нет плана — пересчитать
            if (_currentPlan.IsEmpty) return true;

            // Завершился головной шаг
            if (_currentPlan.Head != null &&
                _currentPlan.Head.Status == ChainStepStatus.Completed)
                return true;

            // Состав объектов изменился
            if (ObjectSetChanged(snapshot)) return true;

            // Fallback: давно не пересчитывали
            _framesSinceRecalc++;
            return _framesSinceRecalc >= MaxFramesWithoutRecalc;
        }

        private bool ObjectSetChanged(BotSceneSnapshot snapshot)
        {
            var currentIds = new System.Collections.Generic.HashSet<int>();
            foreach (var o in snapshot.VisibleObjects) currentIds.Add(o.StableId);
            if (_prevObjectIds.SetEquals(currentIds)) return false;
            _prevObjectIds = currentIds;
            return true;
        }

        private ProjectedState GetTailProjectedState(BotSceneSnapshot snapshot)
        {
            var state = ProjectedState.FromSnapshot(snapshot);
            var tail = _currentPlan.GetTail();
            if (tail.Count == 0) return state;

            var projector = new StateProjector();
            foreach (var step in tail)
                state = projector.Project(state, step, step.TargetObstacle);
            return state;
        }

        // ──────────────── State Checks ────────────────

        /// <summary>Хомяк в состоянии, когда можно давать команды.</summary>
        private static bool IsControllableState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.Run
                || state == HamsterStateEnum.RoofRun;
        }

        // ──────────────── Auto-restart on death ────────────────

        private bool _deathHandled;

        private void HandleDeath()
        {
            if (_deathHandled) return;
            _deathHandled = true;

            DebugManager.DiagLog($"[HamsterBot] Hamster DIED. Actions executed: {_timingPolicy?.ActionsExecuted}");

            if (_autoRestartOnDeath && LevelController.Instance != null)
            {
                StartCoroutine(AutoRestartCoroutine());
            }
        }

        private IEnumerator AutoRestartCoroutine()
        {
            yield return new WaitForSecondsRealtime(_autoRestartDelay);
            if (!IsEnabled) yield break;

            DebugManager.DiagLog("[HamsterBot] Auto-restarting level.");
            _deathHandled = false;
            LevelController.Instance.Replay();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (Instance == this)
                Instance = null;
        }

        // ──────────────── Public API ────────────────

        /// <summary>
        /// Включить/выключить бота (F1).
        /// </summary>
        public void ToggleEnabled()
        {
            if (IsEnabled)
                Disable();
            else
                TryInitAndEnable();
        }

        // ──────────────── Init / Enable / Disable ────────────────

        private void TryInitAndEnable()
        {
            if (!_initialized)
            {
                _hamster = FindObjectOfType<Hamster>();
                if (_hamster == null)
                {
                    Debug.LogWarning("[HamsterBot] Hamster not found in scene. Bot disabled.");
                    return;
                }

                _snapshotBuilder  = new SnapshotBuilder();
                _classifier       = new ObjectClassifier();
                _chainGenerator   = new ChainGenerator();
                _chainScorer      = new ChainScorer();
                _planValidator    = new PlanValidator();
                _planSelector     = new PlanSelector();
                _timingPolicy     = new BotTimingPolicy(this, _hamster);
                _gameManager      = LevelController.Instance?.LevelData?.GameManager;
                _initialized      = true;
                DebugManager.DiagLog("[HamsterBot] Initialized successfully.");
            }

            IsEnabled    = true;
            _currentPlan = new CurrentPlan();
            _prevObjectIds.Clear();
            _timingPolicy.Reset();
            DebugManager.DiagLog("[HamsterBot] ENABLED.");
        }

        private void Disable()
        {
            IsEnabled = false;
            DebugManager.DiagLog("[HamsterBot] DISABLED.");
        }

    }
}
