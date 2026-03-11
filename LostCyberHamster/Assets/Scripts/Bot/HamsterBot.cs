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
                _planner.ScanObstacles(_hamster, _scanRange);
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

            // Мониторинг: через 2 кадра проверяем, перешёл ли Animator в новый state
            StartCoroutine(LogAnimatorStateAfterFrames(action, _actionsExecuted));
        }

        private IEnumerator LogAnimatorStateAfterFrames(BotAction action, int execNum)
        {
            yield return null; // 1 кадр
            yield return null; // 2 кадр

            var transformAnim = _hamster.GetComponentInChildren<TransformAnimatorController>();
            var spriteAnim = _hamster.GetComponentInChildren<SpriteAnimatorController>();
            if (transformAnim != null)
            {
                var a = transformAnim.Animator;
                var info = a.GetCurrentAnimatorStateInfo(0);
                var clips = a.GetCurrentAnimatorClipInfo(0);
                string clipName = clips.Length > 0 ? clips[0].clip.name : "?";
                DebugManager.DiagLog($"[HamsterBot] ANIM_CHECK exec#{execNum} ({action}) +2frames: transformAnim enabled={a.enabled} stateHash={info.shortNameHash} clip='{clipName}' normTime={info.normalizedTime:F2} hamsterState={_hamster.HamsterState.Value}");
            }
            if (spriteAnim != null)
            {
                var a = spriteAnim.GetComponent<Animator>();
                var info = a.GetCurrentAnimatorStateInfo(0);
                var clips = a.GetCurrentAnimatorClipInfo(0);
                string clipName = clips.Length > 0 ? clips[0].clip.name : "?";
                DebugManager.DiagLog($"[HamsterBot] ANIM_CHECK exec#{execNum} ({action}) +2frames: spriteAnim enabled={a.enabled} stateHash={info.shortNameHash} clip='{clipName}' normTime={info.normalizedTime:F2}");
            }
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
