using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
using Sirenix.OdinInspector;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Оркестратор BotV2. Вешается на GameObject в сцене.
    /// Координирует pipeline: Snapshot → Classify → Generate → Select → Execute.
    /// Горячая клавиша F1: вкл/выкл.
    /// Этап 1: один smallNotAliveRoad, SwitchLane / Jump.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
        [Title("BotV2 Settings")]
        [SerializeField] private bool _enabledOnStart = true;

        [SerializeField, Range(5f, 30f)]
        [Tooltip("Дальность сканирования (мировых единиц)")]
        private float _scanRange = 15f;

        [SerializeField]
        public BotLogLevel LogLevel = BotLogLevel.Normal;

        [Title("Runtime"), ReadOnly]
        [ShowInInspector] public bool IsEnabled { get; private set; }

        [ShowInInspector, ReadOnly]
        private string _lastAction = "—";

        // ──────── Pipeline ────────

        private Hamster      _hamster;
        private GameManager  _gameManager;

        private SnapshotBuilder  _snapshotBuilder;
        private ObjectClassifier _classifier;
        private ActionGenerator  _generator;
        private ActionSelector   _selector;
        private StepExecutor     _executor;

        private bool      _initialized;
        private ChainStep _activeStep;

        // ──────── Lifecycle ────────

        private void Start()
        {
            if (!BotV2Bootstrap.UseBotV2AsPrimary)
            {
                enabled = false;
                return;
            }

            if (_enabledOnStart)
                EnableAsPrimary();
        }

        /// <summary>
        /// Включает BotV2 в режиме основного бота.
        /// Вызывается bootstrap-инициализацией после загрузки сцены.
        /// </summary>
        public void EnableAsPrimary()
        {
            enabled = true;
            IsEnabled = true;

            if (!_initialized)
                TryInit();
        }

        private void Update()
        {
            if (IsTogglePressed()) Toggle();

            if (!IsEnabled || !_initialized) return;
            if (_gameManager == null || _gameManager.State != GameState.PLAYING) return;

            var state = _hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Dead) return;
            if (state != HamsterStateEnum.Run) return;

            BotLogger.Level = LogLevel;

            // Исполняем активный шаг или планируем новый
            if (_activeStep != null)
            {
                if (_activeStep.Status == ChainStepStatus.Completed)
                {
                    _executor.ClearStep();
                    _activeStep = null;
                }
                else
                {
                    _executor.TryExecute();
                    return;
                }
            }

            RunPipeline();
        }

            private static bool IsTogglePressed()
            {
        #if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
        #elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.F1);
        #else
                return false;
        #endif
            }

        // ──────── Pipeline ────────

        private void RunPipeline()
        {
            var snapshot = _snapshotBuilder.Build(_hamster, _scanRange);
            LogSnapshot(snapshot);

            _classifier.Classify(snapshot);
            LogClassify(snapshot);

            var candidates = _generator.Generate(snapshot);
            LogGenerate(candidates);

            var best = _selector.Select(candidates);
            if (best == null)
            {
                BotLogger.Log(BotLogLevel.Verbose, "[SELECT] no safe actions → None");
                return;
            }

            _lastAction = best.Action.ToString();
            BotLogger.Log(BotLogLevel.Normal,
                $"[SELECT] chose {best.Action} (cost={best.EnergyCost}, reason=\"{best.Reason}\")");

            _activeStep = best;
            _executor.SetStep(best);
        }

        // ──────── Init / Toggle ────────

        private void Toggle()
        {
            if (IsEnabled)
            {
                IsEnabled = false;
                DebugManager.DiagLog("[BotOrchestrator] Disabled");
                return;
            }
            IsEnabled = true;
            if (!_initialized) TryInit();
        }

        private void TryInit()
        {
            _hamster     = FindObjectOfType<Hamster>();
            _gameManager = FindObjectOfType<GameManager>();

            if (_hamster == null || _gameManager == null)
            {
                DebugManager.DiagLog("[BotOrchestrator] Init failed — Hamster or GameManager not found");
                return;
            }

            _snapshotBuilder = new SnapshotBuilder();
            _classifier      = new ObjectClassifier();
            _generator       = new ActionGenerator();
            _selector        = new ActionSelector();
            _executor        = new StepExecutor(_hamster);

            // Логирование урона
            _hamster.DamageEvent.Subscribe(OnDamage);

            _initialized = true;
            IsEnabled    = true;
            DebugManager.DiagLog("[BotOrchestrator] Initialized (Stage 1)");
        }

        private void OnDestroy()
        {
            if (_hamster != null)
                _hamster.DamageEvent.Unsubscribe(OnDamage);
        }

        // ──────── Damage log ────────

        private void OnDamage()
        {
            var step = _activeStep;
            string stepInfo = step == null
                ? "none"
                : $"{step.Action} status={step.Status} executeAt={step.ExecuteAtDistance:F1}";

            float threatDist = GetNearestThreatDist();

            DebugManager.DiagLog(
                $"[DAMAGE] ===\n" +
                $"  hamster: lane={(_hamster.IsOnBottomLine.Value ? "bottom" : "top")}" +
                $" energy={_hamster.Energy.Value} lives={_hamster.Lives.Value}\n" +
                $"  active step: {stepInfo}\n" +
                $"  nearest threat dist: {threatDist:F2}\n" +
                $"[DAMAGE] ===");
        }

        private float GetNearestThreatDist()
        {
            var spawner = Assets.Scripts.System.ObstacleSpawner.Instance;
            if (spawner == null) return -1f;

            float minDist = float.MaxValue;
            foreach (var inst in spawner.SpawnedObstacles)
            {
                if (inst?.ObstacleScript == null) continue;
                if (inst.ObstacleScript.ObstacleType.ObstacleTypeEnum !=
                    Assets.Scripts.Common.Models.ObstacleTypeEnum.smallNotAliveRoad) continue;

                float leftX = inst.ObstacleScript.transform.position.x
                            - inst.ObstacleScript.ColliderWidth * 0.5f;
                float dist = leftX - _hamster.RightX;
                if (dist < minDist) minDist = dist;
            }
            return minDist == float.MaxValue ? -1f : minDist;
        }

        // ──────── Verbose logging ────────

        private void LogSnapshot(BotSceneSnapshot s)
        {
            BotLogger.Log(BotLogLevel.Verbose,
                $"[SNAPSHOT] hamster=(lane={(s.HamsterOnBottom ? "bottom" : "top")} energy={s.Energy} lives={s.Lives}) visible={s.VisibleObjects.Count}");
        }

        private void LogClassify(BotSceneSnapshot s)
        {
            if (BotLogger.Level != BotLogLevel.Verbose) return;
            for (int i = 0; i < s.VisibleObjects.Count; i++)
            {
                var obs = s.VisibleObjects[i];
                BotLogger.Log(BotLogLevel.Verbose,
                    $"[CLASSIFY] #{i} {obs.Type} dist={obs.DistanceToHamster:F1} lane={(obs.IsTopLane ? "top" : "bottom")} → {obs.Category}");
            }
        }

        private void LogGenerate(List<ChainStep> steps)
        {
            if (BotLogger.Level != BotLogLevel.Verbose) return;
            foreach (var s in steps)
                BotLogger.Log(BotLogLevel.Verbose,
                    $"[GENERATE] action={s.Action} cost={s.EnergyCost} dist={s.ExecuteAtDistance:F1}");
        }
    }
}
