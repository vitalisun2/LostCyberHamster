using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
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

        [Title("Runtime Info"), ReadOnly]
        [ShowInInspector] public bool IsEnabled { get; private set; }

        [ShowInInspector, ReadOnly]
        private string _lastDecisionText = "—";

        [ShowInInspector, ReadOnly]
        private int _actionsExecuted;

        // ──────────────── Internals ────────────────

        private Hamster _hamster;
        private BotChainPlanner _planner;
        private bool _initialized;

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
            yield return new WaitForSeconds(2f);

            if (!IsEnabled) yield break;

            TryInitAndEnable();
            TrySkipIntro();
            DebugManager.DiagLog("[HamsterBot] Re-initialized after scene load.");
        }

        private void TrySkipIntro()
        {
            var intro = FindObjectOfType<Intro>();
            if (intro != null)
            {
                intro.SkipIntro();
                DebugManager.DiagLog("[HamsterBot] Auto-skipped intro.");
            }
        }

        private void Update()
        {
            if (!IsEnabled || !_initialized) return;
            if (_hamster == null || _hamster.HamsterState.Value == HamsterStateEnum.Dead)
                return;

            // TODO: Chain Planner — будет реализован в Этапах 2-8
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
                    _hamster.SuperJumpRequest.Invoke();
                    break;

                case BotAction.RoofJump:
                    _hamster.RoofJumpRequest.Invoke();
                    break;

                case BotAction.SuperRoofJump:
                    _hamster.SuperRoofJumpRequest.Invoke();
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
            DebugManager.DiagLog(
                $"[HamsterBot] Action #{_actionsExecuted}: {action} | state={_hamster.HamsterState.Value}");
        }
    }
}
