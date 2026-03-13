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
        private int _actionsExecuted;

        // ──────────────── Internals ────────────────

        private Hamster _hamster;
        private BotChainPlanner _planner;
        private SnapshotBuilder _snapshotBuilder;
        private GameManager _gameManager;
        private bool _initialized;

        // Dirty flag
        private bool _dirty = true;
        private int _framesSinceRecalc;
        private const int MaxFramesWithoutRecalc = 10;
        private int _prevObstacleCount;
        private HamsterStateEnum _prevState;
        private int _prevEnergy;
        private int _prevUlta;

        // Watchdog: обнаружение застревания в неконтролируемом состоянии
        private float _uncontrollableStartTime;
        private float _lastWatchdogWarnTime;
        private bool _inUncontrollableState;
        private const float UncontrollableWarnTime = 2f;
        private float _lastPeriodicLogTime;

        // World-shift кэш для проверки прыжков через CollisionUtils
        private float _jumpWorldShift = -1f;
        private float _roofJumpWorldShift = -1f;

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
            _jumpWorldShift = -1f;
            _roofJumpWorldShift = -1f;

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
            if (!IsEnabled || !_initialized) return;
            if (_hamster == null) return;

            // Ждём, пока игра реально запустится (после интро)
            if (_gameManager == null || _gameManager.State != GameState.PLAYING)
                return;

            if (_hamster.HamsterState.Value == HamsterStateEnum.Dead)
            {
                HandleDeath();
                return;
            }

            // Не действуем в процессе прыжка/смены линии — ждём приземления
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

            CheckDirtyFlag();

            if (_dirty)
            {
                var snapshot = _snapshotBuilder.Build(_hamster, _scanRange);
                _planner.LoadFromSnapshot(snapshot);
                bool chainBuilt = _planner.BuildChain(_hamster);
                _dirty = false;
                _framesSinceRecalc = 0;

                // Периодический лог: состояние и результат сканирования (раз в 3 сек)
                if (Time.time - _lastPeriodicLogTime > 3f)
                {
                    _lastPeriodicLogTime = Time.time;
                    DebugManager.DiagLog($"[HamsterBot] SCAN: {_planner.Obstacles.Count} obs, chain={chainBuilt}, state={currentState} pos={_hamster.RightX:F2}");
                }
            }

            // Есть ли шаг для выполнения?
            if (_planner.Chain.Count > 0)
            {
                var step = _planner.Chain[0];
                if (step.Action != BotAction.None)
                {
                    // Проверяем тайминг: объект достаточно близко?
                    if (step.TargetObstacleIndex >= 0 &&
                        step.TargetObstacleIndex < _planner.Obstacles.Count)
                    {
                        var target = _planner.Obstacles[step.TargetObstacleIndex];
                        if (target.DistanceToHamster <= step.ExecuteAtDistance)
                        {
                            if (ShouldDelayJumpOver(step, target))
                                return; // ждём — CollisionUtils показывает overlap

                            if (ShouldDelayJumpOn(step, target))
                                return; // ждём — центр ещё не попадает внутрь цели

                            ExecuteAction(step.Action);
                            _dirty = true; // пересчитать после действия
                        }
                    }
                    else
                    {
                        // Шаг без конкретной цели — выполнить сразу
                        ExecuteAction(step.Action);
                        _dirty = true;
                    }
                }
            }
        }

        private void CheckDirtyFlag()
        {
            _framesSinceRecalc++;

            // Fallback
            if (_framesSinceRecalc >= MaxFramesWithoutRecalc)
            {
                _dirty = true;
                return;
            }

            // Состояние изменилось
            var curState = _hamster.HamsterState.Value;
            if (curState != _prevState)
            {
                _prevState = curState;
                _dirty = true;
                return;
            }

            // Энергия изменилась
            int curEnergy = _hamster.Energy.Value;
            if (curEnergy != _prevEnergy)
            {
                _prevEnergy = curEnergy;
                _dirty = true;
                return;
            }

            // Ульта готова
            int curUlta = _hamster.UltaChargeAmount.Value;
            if (curUlta != _prevUlta)
            {
                _prevUlta = curUlta;
                _dirty = true;
                return;
            }

            // Изменилось количество объектов на сцене
            var spawner = ObstacleSpawner.Instance;
            int count = spawner != null ? spawner.SpawnedObstacles.Count : 0;
            if (count != _prevObstacleCount)
            {
                _prevObstacleCount = count;
                _dirty = true;
            }
        }

        // ──────────────── Jump-Over Collision Check ────────────────

        /// <summary>
        /// Для JumpOn (Target) — ждёт момент, когда центр хомяка попадёт внутрь цели.
        /// Использует CollisionUtils.IsHamsterCenterInsideObstacleAtShift —
        /// ту же проверку, что JumpMechanics.HandleSmallAlive для JumpOnObstacle.
        /// </summary>
        private bool ShouldDelayJumpOn(ChainStep step, ObstacleInfo target)
        {
            if (step.Action != BotAction.Jump && step.Action != BotAction.RoofJump)
                return false;

            if (step.Reason == null || !step.Reason.StartsWith("JumpOn"))
                return false;

            var obsRef = target.ObstacleRef;
            if (obsRef == null) return false;

            EnsureWorldShiftsCached();
            float worldShift = step.Action == BotAction.RoofJump
                ? _roofJumpWorldShift
                : _jumpWorldShift;
            if (worldShift <= 0f) return false;

            // Та же проверка, что JumpMechanics: rightTol = hamsterWidth * 0.2
            float rightTol = _hamster.ColliderWidth * 0.2f;
            bool wouldLandOn = CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                _hamster.transform, worldShift, obsRef, rightTol);

            if (wouldLandOn) return false; // идеальный момент — прыгаем!

            // Failsafe: слишком близко — прыгаем всё равно (лучше damage, чем crash)
            float realDist = obsRef.transform.position.x
                - obsRef.ColliderWidth * 0.5f - _hamster.RightX;
            return realDist > 0.5f;
        }

        /// <summary>
        /// Проверяет, приведёт ли прыжок прямо сейчас к наложению на препятствие.
        /// Использует CollisionUtils — ту же логику коллайдеров, что JumpMechanics.
        /// Не применяется к Target-прыжкам (JumpOn) — там цель приземлиться НА препятствие.
        /// </summary>
        private bool ShouldDelayJumpOver(ChainStep step, ObstacleInfo target)
        {
            if (step.Action != BotAction.Jump && step.Action != BotAction.RoofJump)
                return false;

            // JumpOn Target — не задерживать: хотим приземлиться НА цель, а не перепрыгнуть
            if (step.Reason != null && step.Reason.StartsWith("JumpOn"))
                return false;

            // Только для перепрыгиваемых мелких препятствий
            if (target.Type != ObstacleTypeEnum.smallNotAliveRoad &&
                target.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof &&
                target.Type != ObstacleTypeEnum.smallAlive)
                return false;

            var obsRef = target.ObstacleRef;
            if (obsRef == null) return false;

            EnsureWorldShiftsCached();
            float worldShift = step.Action == BotAction.RoofJump
                ? _roofJumpWorldShift
                : _jumpWorldShift;
            if (worldShift <= 0f) return false;

            bool wouldOverlap = CollisionUtils.IsOverlapAtShift(
                _hamster.transform, _hamster.ColliderWidth, worldShift, obsRef);

            if (!wouldOverlap) return false; // безопасно — прыгаем

            // Failsafe: препятствие вплотную — прыгаем всё равно
            float realDist = obsRef.transform.position.x
                - obsRef.ColliderWidth * 0.5f - _hamster.RightX;
            return realDist > 0.1f;
        }

        private void EnsureWorldShiftsCached()
        {
            if (_jumpWorldShift >= 0f) return;

            var ctrl = _hamster.GetComponentInChildren<TransformAnimatorController>();
            if (ctrl == null) return;

            _jumpWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, "transform_jump");
            _roofJumpWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, "transform_roof_jump");

            DebugManager.DiagLog(
                $"[HamsterBot] Cached worldShifts: jump={_jumpWorldShift:F2}, roofJump={_roofJumpWorldShift:F2}");
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

            DebugManager.DiagLog($"[HamsterBot] Hamster DIED. Actions executed: {_actionsExecuted}");

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

                _planner = new BotChainPlanner();
                _snapshotBuilder = new SnapshotBuilder();
                _gameManager = LevelController.Instance?.LevelData?.GameManager;
                _initialized = true;
                DebugManager.DiagLog("[HamsterBot] Initialized successfully.");
            }

            IsEnabled = true;
            _actionsExecuted = 0;
            DebugManager.DiagLog("[HamsterBot] ENABLED.");
        }

        private void Disable()
        {
            IsEnabled = false;
            DebugManager.DiagLog("[HamsterBot] DISABLED.");
        }

        // ──────────────── Action Execution ────────────────

        private void ExecuteAction(BotAction action)
        {
            switch (action)
            {
                case BotAction.Jump:
                    if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                        _hamster.RoofJumpRequest.Invoke();
                    else
                        _hamster.JumpRequest.Invoke();
                    break;

                case BotAction.SuperJump:
                    // SuperJump = double-tap: сначала Jump (→ hamster_jump state),
                    // потом SuperJump (transition из hamster_jump → transform_super_jump)
                    _hamster.JumpRequest.Invoke();
                    StartCoroutine(DelayedSuperJump());
                    break;

                case BotAction.RoofJump:
                    _hamster.RoofJumpRequest.Invoke();
                    break;

                case BotAction.SuperRoofJump:
                    // SuperRoofJump = double-tap on roof: сначала RoofJump, потом SuperRoofJump
                    _hamster.RoofJumpRequest.Invoke();
                    StartCoroutine(DelayedSuperRoofJump());
                    break;

                case BotAction.SwitchLane:
                    _hamster.TapRequest.Invoke();
                    break;

                case BotAction.UseUlta:
                    _hamster.UltaEvent.Invoke();
                    break;
            }

            _actionsExecuted++;
            _lastDecisionText = action.ToString();

            // Логируем действие + контекст ближайших объектов
            var obs = _planner?.Obstacles;
            string nearInfo = "";
            if (obs != null)
            {
                for (int i = 0; i < obs.Count && i < 3; i++)
                {
                    var o = obs[i];
                    if (o.DistanceToHamster < -1f || o.DistanceToHamster > 6f) continue;
                    nearInfo += $" | {o.Type}({o.Category}) d={o.DistanceToHamster:F2}";
                }
            }
            DebugManager.DiagLog(
                $"[HamsterBot] EXEC #{_actionsExecuted}: {action} state={_hamster.HamsterState.Value} pos={_hamster.RightX:F2}{nearInfo}");
        }

        /// <summary>
        /// SuperJump = double-tap. Ждём 1 кадр (чтобы Animator перешёл в hamster_jump),
        /// затем вызываем SuperJumpRequest.
        /// </summary>
        private IEnumerator DelayedSuperJump()
        {
            yield return null;
            _hamster.SuperJumpRequest.Invoke();
        }

        /// <summary>
        /// SuperRoofJump = double-tap on roof. Ждём 1 кадр, затем SuperRoofJumpRequest.
        /// </summary>
        private IEnumerator DelayedSuperRoofJump()
        {
            yield return null;
            _hamster.SuperRoofJumpRequest.Invoke();
        }
    }
}
