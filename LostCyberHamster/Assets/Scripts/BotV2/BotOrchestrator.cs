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
        private float _nextInitRetryTime;

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

            if (!IsEnabled) return;

            // BotV2 может создаваться в Bootstrap-сцене раньше, чем появятся Hamster/GameManager.
            // Повторяем TryInit до успешной инициализации.
            if (!_initialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryInit();
                    _nextInitRetryTime = Time.time + 0.5f;
                }
                return;
            }

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
                    // Если шаг был отменён (целевая линия стала опасной) — сразу перепланируем
                }
                else
                {
                    _executor.TryExecute();
                    // Проверяем, не отменил ли executor шаг прямо сейчас
                    if (_executor.WasCancelled)
                    {
                        _executor.ClearStep();
                        _activeStep = null;
                        // Провалимся ниже в RunPipeline для перепланирования
                    }
                    else
                    {
                        return;
                    }
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

        /// <summary>
        /// Переключение бота через глобальный хоткей (F1) из KeyboardMechanics.
        /// </summary>
        public void ToggleEnabledFromHotkey()
        {
            Toggle();
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

            string hamsterLane = _hamster.IsOnBottomLine.Value ? "bottom" : "top";
            string killerInfo = FindNearestThreatInfo();

            DebugManager.DiagLog(
                $"[DAMAGE] ===\n" +
                $"  hamster: lane={hamsterLane} state={_hamster.HamsterState.Value}" +
                $" energy={_hamster.Energy.Value} lives={_hamster.Lives.Value}\n" +
                $"  active step: {stepInfo}\n" +
                $"  killer (nearest same-lane threat): {killerInfo}\n" +
                $"[DAMAGE] ===");
        }

        private string FindNearestThreatInfo()
        {
            var spawner = Assets.Scripts.System.ObstacleSpawner.Instance;
            if (spawner == null) return "spawner=null";

            bool hamsterOnBottom = _hamster.IsOnBottomLine.Value;
            float hamsterRightX = _hamster.RightX;

            string nearestSameLane = "none";
            float minDistSame = float.MaxValue;
            string nearestAnyLane = "none";
            float minDistAny = float.MaxValue;

            foreach (var inst in spawner.SpawnedObstacles)
            {
                if (inst?.ObstacleScript == null) continue;
                var obs = inst.ObstacleScript;
                if (obs.ObstacleType.ObstacleTypeEnum !=
                    Assets.Scripts.Common.Models.ObstacleTypeEnum.smallNotAliveRoad) continue;

                float leftX = obs.transform.position.x - obs.ColliderWidth * 0.5f;
                float dist = leftX - hamsterRightX;
                string lane = obs.ObstacleType.IsTop ? "top" : "bottom";
                string info = $"{obs.ObstacleType.ObstacleTypeEnum} lane={lane} dist={dist:F2}";

                if (Mathf.Abs(dist) < Mathf.Abs(minDistAny))
                {
                    minDistAny = dist;
                    nearestAnyLane = info;
                }

                bool sameLane = (hamsterOnBottom && !obs.ObstacleType.IsTop) ||
                                (!hamsterOnBottom && obs.ObstacleType.IsTop);
                if (sameLane && Mathf.Abs(dist) < Mathf.Abs(minDistSame))
                {
                    minDistSame = dist;
                    nearestSameLane = info;
                }
            }

            return nearestSameLane != "none"
                ? nearestSameLane
                : $"no same-lane threat, nearest any: {nearestAnyLane}";
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
