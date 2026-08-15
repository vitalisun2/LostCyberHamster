#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using LostCyberHamster.UI;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.DevTools.SkateboardTesting
{
    /// <summary>
    /// Управляет подготовкой Skateboard, scripted scenarios и passive guided checks.
    /// </summary>
    public sealed class SkateboardTestingRunner
    {
        private const int _skateboardId = 3;
        private const float _betweenGroupsDelay = 0.5f;
        private const float _timeoutTolerance = 0.1f;

        private static readonly ObstacleTypeEnum[] _physicalTypes =
        {
            ObstacleTypeEnum.smallAlive,
            ObstacleTypeEnum.bigAlive,
            ObstacleTypeEnum.smallNotAliveRoad,
            ObstacleTypeEnum.smallNotAliveRoadAndRoof,
            ObstacleTypeEnum.bigNotAlive,
            ObstacleTypeEnum.mediumNotAlive,
        };

        private static readonly string[] _physicalLabels =
        {
            "smallAlive",
            "bigAlive",
            "smallNotAliveRoad",
            "smallNotAliveRoadAndRoof",
            "bigNotAlive side",
            "mediumNotAlive side",
        };

        private readonly PlayerExperienceService _experienceService = new();
        private readonly List<int> _observedImpactDepths = new(3);
        private readonly List<ChecklistItem> _checklist = new(6);

        private Hamster _hamster;
        private SkateboardAttack _attack;
        private GameManager _gameManager;
        private RunnerMode _mode;
        private LaneCheckStage _laneStage;
        private int[] _comboGroups = Array.Empty<int>();
        private int[] _expectedImpactDepths = Array.Empty<int>();
        private int _groupIndex;
        private int _cycleInGroup;
        private int _cyclesScheduled;
        private float _nextGroupAt;
        private float _timeoutStartedAt;
        private int _timeoutInitialBudget;
        private bool _timeoutWaitingStayedTrue;
        private bool _timeoutBudgetStayedUntouched;
        private bool _waitingForQueuedCycle;
        private bool _waitingForRide;
        private bool _waitingForCompletion;
        private bool _landingHandled;
        private bool _useSuperJump;
        private bool _onRoof;
        private bool _pausedByTool;
        private bool _lastObservedLane;
        private bool _laneStart;
        private bool _laneTarget;
        private string _scenarioTitle = string.Empty;
        private string _instruction = string.Empty;
        private string _status = "Запустите Bootstrap. Подготовку выполняйте в Menu.";

        private SkateboardTestingRunner()
        {
        }

        public static SkateboardTestingRunner Shared { get; } = new();

        public event Action Changed;

        public enum ChecklistState
        {
            Pending,
            Pass,
            Fail,
        }

        public readonly struct ChecklistItem
        {
            public ChecklistItem(string label, ChecklistState state, string details = "")
            {
                Label = label;
                State = state;
                Details = details;
            }

            public string Label { get; }
            public ChecklistState State { get; }
            public string Details { get; }
        }

        public bool IsBusy => _mode != RunnerMode.None;
        public bool UseSuperJump => _useSuperJump;
        public bool OnRoof => _onRoof;
        public string Status => _status;
        public string Instruction => _instruction;
        public IReadOnlyList<ChecklistItem> Checklist => _checklist;
        public string LiveStatus => BuildLiveStatus();
        public string PauseButtonLabel => IsPausedByTool ? "Resume" : "Pause";
        private bool IsPausedByTool =>
            _pausedByTool &&
            TryGetGameManager(out GameManager manager) &&
            manager.State == GameState.PAUSED;

        public bool CanPrepare =>
            Application.isPlaying && !IsBusy &&
            GameDataManager.PlayerData != null &&
            SuperAttackService.TryGet(_skateboardId, out _);
        public bool CanRunScenario =>
            !IsBusy && TryResolvePlayingGameplay(out _, out _, out _);
        public bool CanStartGuidedCheck => CanRunScenario;
        public bool CanTogglePause =>
            TryGetGameManager(out GameManager manager) &&
            (manager.State == GameState.PLAYING ||
             IsPausedByTool && manager.State == GameState.PAUSED);
        public bool CanStopCheck =>
            IsBusy ||
            (TryResolveGameplay(out _, out SkateboardAttack attack, out _) &&
             attack.IsActive);

        /// <summary>
        /// Выдаёт ровно недостающий XP, проверяет unlock и выбирает Skateboard.
        /// </summary>
        public void PrepareUnlockAndSelectSkateboard()
        {
            if (!CanPrepare)
            {
                SetStatus("Подготовка недоступна: дождитесь PlayerData и каталога.");
                return;
            }

            try
            {
                PlayerData playerData = GameDataManager.PlayerData;
                if (!SuperAttackService.TryGet(_skateboardId, out SuperAttackData skateboard))
                    throw new InvalidOperationException("Skateboard ID 3 отсутствует в каталоге.");

                int beforeLevel = playerData.PlayerLevel;
                int beforeExperience = playerData.ExperiencePoints;
                int missingExperience = CalculateMissingExperience(
                    playerData, skateboard.RequiredPlayerLevel);
                if (missingExperience > 0)
                    _experienceService.GrantExperienceForTesting(playerData, missingExperience);

                if (!SuperAttackService.IsUnlocked(_skateboardId, playerData.PlayerLevel))
                    throw new InvalidOperationException("Skateboard не открылся после начисления XP.");
                if (!SuperAttackService.TrySelect(_skateboardId) ||
                    SuperAttackService.ActiveSuperAttackId != _skateboardId)
                {
                    throw new InvalidOperationException("SuperAttackService не выбрал Skateboard.");
                }

                UIManager.OnRepaintScreen?.Invoke();
                string runtimeNote = TryFindHamster(out _)
                    ? " Текущий Hamster не заменяется: войдите в следующий уровень."
                    : string.Empty;
                SetStatus(
                    $"PASS: Skateboard открыт и выбран. Level {beforeLevel}, " +
                    $"XP {beforeExperience} -> Level {playerData.PlayerLevel}, " +
                    $"XP {playerData.ExperiencePoints}; добавлено {missingExperience} XP." +
                    runtimeNote);
            }
            catch (Exception exception)
            {
                SetStatus($"FAIL: {exception.Message}", isError: true);
            }
        }

        /// <summary>
        /// Запускает passive timeout check без отправки jump input.
        /// </summary>
        public void RunTimeoutCheck()
        {
            if (!TryStartCheckWithMode(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                return;
            }

            _hamster = hamster;
            _attack = attack;
            _gameManager = gameManager;
            _mode = RunnerMode.Timeout;
            _scenarioTitle = "Timeout 10 gameplay s";
            _instruction = "Ничего не нажимайте. Runner ждёт 10 gameplay seconds.";
            _timeoutStartedAt = Time.time;
            _timeoutInitialBudget = attack.JumpsRemaining;
            _timeoutWaitingStayedTrue = attack.IsWaitingForFirstJump;
            _timeoutBudgetStayedUntouched =
                _timeoutInitialBudget == SkateboardAttack.DefaultJumpBudget;
            SetChecklist(new[]
            {
                "Enter Mode",
                "First-jump waiting stayed active",
                "Jump budget untouched",
                "Skateboard actor disabled",
                "Normal actor enabled",
                "Timeout",
            });
            SetChecklistResult(0, true, "Skateboard active");
            attack.LandingImpact += OnLandingImpact;
            SetStatus($"PASS: Enter Mode. RUNNING: {_scenarioTitle}. {_instruction}");
        }

        public void RunOnePlusOnePlusOneScenario() =>
            StartComboScenario("1+1+1", new[] { 1, 1, 1 }, new[] { 1, 1, 1 });
        public void RunTwoPlusOneScenario() =>
            StartComboScenario("2+1", new[] { 2, 1 }, new[] { 1, 2, 1 });
        public void RunOnePlusTwoScenario() =>
            StartComboScenario("1+2", new[] { 1, 2 }, new[] { 1, 1, 2 });
        public void RunThreeComboScenario() =>
            StartComboScenario("3 Combo", new[] { 3 }, new[] { 1, 2, 3 });

        public void StartRideDamageCheck() => StartCollisionCheck(
            RunnerMode.RideDamage,
            "Ride Damage",
            "Столкнитесь боком с pending obstacle.");

        public void StartJumpCollisionCheck() => StartCollisionCheck(
            RunnerMode.JumpCollision,
            "Jump Collision",
            "Начните jump и попадите в pending obstacle.");

        /// <summary>
        /// Запускает guided watcher lane shift без scripted input.
        /// </summary>
        public void StartLaneShiftCheck()
        {
            if (!TryStartCheckWithMode(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                return;
            }

            _hamster = hamster;
            _attack = attack;
            _gameManager = gameManager;
            _mode = RunnerMode.LaneShift;
            _scenarioTitle = "Lane Shift";
            _laneStage = LaneCheckStage.AwaitRideTap;
            _lastObservedLane = hamster.IsOnBottomLine.Value;
            SetChecklist(new[]
            {
                "Ride tap starts shift and finishes on other lane",
                "Jump during active shift is accepted",
                "Tap after jump start is rejected",
            });
            hamster.TapRequest.Subscribe(OnGuidedTapRequested);
            hamster.JumpRequest.Subscribe(OnGuidedJumpRequested);
            _instruction = "В Ride смените линию и дождитесь конца shift.";
            SetStatus($"PASS: Enter Mode. RUNNING: {_scenarioTitle}. {_instruction}");
        }

        public void SetUseSuperJump(bool value)
        {
            if (IsBusy) return;
            _useSuperJump = value;
            SetStatus(value
                ? "Scripted cycles используют Super Jump."
                : "Scripted cycles используют normal Jump.");
        }

        public void SetOnRoof(bool value)
        {
            if (IsBusy) return;
            _onRoof = value;
            SetStatus(value
                ? "Scripted surface gate: stable Roof/RoofRun."
                : "Scripted surface gate: stable road Ride/Run.");
        }

        /// <summary>
        /// Переключает PLAYING и созданную этим инструментом PAUSED-сессию.
        /// </summary>
        public void TogglePause()
        {
            if (!CanTogglePause || !TryGetGameManager(out GameManager manager))
                return;

            if (IsPausedByTool)
            {
                manager.Resume();
                _pausedByTool = false;
                SetStatus(IsBusy
                    ? $"RUNNING: {_scenarioTitle}. {_instruction}"
                    : "Game resumed.");
                return;
            }

            manager.Pause();
            _pausedByTool = true;
            SetStatus(IsBusy
                ? $"PAUSED: {_scenarioTitle}. Check сохранён."
                : "Game paused.");
        }

        /// <summary>
        /// Останавливает active runner и завершает Skateboard mode.
        /// </summary>
        public void StopCheck()
        {
            SkateboardAttack attack = _attack;
            StopActiveCheck(clearChecklist: false);
            if (attack == null &&
                TryResolveGameplay(out _, out SkateboardAttack resolved, out _))
            {
                attack = resolved;
            }

            if (attack?.IsActive == true) attack.Complete();
            SetStatus("STOPPED: Check остановлен; Skateboard mode завершён.");
        }

        /// <summary>
        /// Продвигает active check только в gameplay time и по live runtime state.
        /// </summary>
        public void Tick()
        {
            if (!IsBusy) return;
            if (_gameManager == null || _attack == null || _hamster == null)
            {
                FailActiveCheck("Gameplay runtime потерян.");
                return;
            }

            if (_gameManager.State == GameState.PAUSED) return;
            if (_gameManager.State != GameState.PLAYING)
            {
                FailActiveCheck($"GameState стал {_gameManager.State}.");
                return;
            }

            switch (_mode)
            {
                case RunnerMode.Combo: TickComboScenario(); break;
                case RunnerMode.Timeout: TickTimeoutCheck(); break;
                case RunnerMode.RideDamage:
                case RunnerMode.JumpCollision:
                    TickGuidedModeLease();
                    break;
                case RunnerMode.LaneShift:
                    TickGuidedModeLease();
                    TickLaneShiftCheck();
                    break;
            }
        }

        public void HandlePlayModeStarted()
        {
            _pausedByTool = false;
            StopActiveCheck(clearChecklist: true);
            SetStatus("Play Mode готов. В Menu подготовьте и выберите Skateboard.");
        }

        public void HandlePlayModeStopped()
        {
            _pausedByTool = false;
            StopActiveCheck(clearChecklist: true);
            SetStatus("Play Mode остановлен.");
        }

        private void StartComboScenario(
            string title,
            int[] comboGroups,
            int[] expectedImpactDepths)
        {
            if (!TryResolvePlayingGameplay(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                SetStatus(GetGameplayUnavailableStatus());
                return;
            }

            if (IsBusy)
            {
                SetStatus("Сначала завершите active check.");
                return;
            }

            if (attack.IsActive)
            {
                ProbeRejectedActivation(hamster, attack);
                return;
            }

            if (!ValidateScriptedSurface(hamster, out string instruction))
            {
                SetStatus(instruction);
                return;
            }

            if (!TryEnterMode(hamster, attack, out string error))
            {
                SetStatus($"FAIL: {error}", isError: true);
                return;
            }

            _hamster = hamster;
            _attack = attack;
            _gameManager = gameManager;
            _mode = RunnerMode.Combo;
            _comboGroups = comboGroups;
            _expectedImpactDepths = expectedImpactDepths;
            _observedImpactDepths.Clear();
            _groupIndex = 0;
            _cycleInGroup = 0;
            _cyclesScheduled = 0;
            _nextGroupAt = Time.time;
            _waitingForQueuedCycle = false;
            _waitingForRide = false;
            _waitingForCompletion = false;
            _landingHandled = false;
            _scenarioTitle = title;
            _instruction = "Runner отправляет реальные jump requests и ждёт FSM.";
            attack.LandingImpact += OnLandingImpact;
            SetStatus(
                $"PASS: Enter Mode. RUNNING: {title}; " +
                $"jump={(_useSuperJump ? "Super" : "Normal")}, " +
                $"surface={(_onRoof ? "Roof" : "Road")}.");
        }

        private void TickComboScenario()
        {
            if (!_attack.IsActive)
            {
                if (_waitingForCompletion &&
                    _cyclesScheduled == SkateboardAttack.DefaultJumpBudget)
                {
                    ValidateAndPassComboScenario();
                }
                else
                {
                    FailActiveCheck(
                        $"Mode завершился раньше: cycles={_cyclesScheduled}/" +
                        $"{SkateboardAttack.DefaultJumpBudget}.");
                }

                return;
            }

            if (_waitingForCompletion) return;
            if (_waitingForQueuedCycle)
            {
                if (_attack.IsJumping)
                {
                    _waitingForQueuedCycle = false;
                    _landingHandled = false;
                    SetStatus(
                        $"RUNNING: {_scenarioTitle}; cycle {_cyclesScheduled}/3, " +
                        $"combo {_attack.ComboDepth}.");
                }
                return;
            }

            if (_waitingForRide)
            {
                if (!_attack.IsRiding) return;
                _waitingForRide = false;
                _groupIndex++;
                _cycleInGroup = 0;
                _landingHandled = false;
                _nextGroupAt = Time.time + _betweenGroupsDelay;
                return;
            }

            if (_cycleInGroup == 0)
            {
                if (!_attack.IsRiding || Time.time < _nextGroupAt) return;
                if (!RequestJumpCycle())
                {
                    FailActiveCheck("Первый jump группы отклонён runtime.");
                    return;
                }

                _cycleInGroup = 1;
                _cyclesScheduled++;
                _landingHandled = false;
                SetStatus($"RUNNING: {_scenarioTitle}; cycle {_cyclesScheduled}/3.");
                return;
            }

            if (!_attack.IsLanding || _landingHandled) return;
            _landingHandled = true;
            int groupSize = _comboGroups[_groupIndex];
            if (_cycleInGroup < groupSize)
            {
                if (!RequestJumpCycle())
                {
                    FailActiveCheck("Queued jump группы отклонён runtime.");
                    return;
                }

                _cycleInGroup++;
                _cyclesScheduled++;
                _waitingForQueuedCycle = true;
                SetStatus($"RUNNING: {_scenarioTitle}; queued cycle {_cyclesScheduled}/3.");
                return;
            }

            if (_groupIndex == _comboGroups.Length - 1)
            {
                if (_attack.JumpsRemaining != 0)
                {
                    FailActiveCheck(
                        $"Final impact оставил budget={_attack.JumpsRemaining}.");
                    return;
                }
                _waitingForCompletion = true;
                SetStatus($"RUNNING: {_scenarioTitle}; ждём final landing tail.");
            }
            else
            {
                _waitingForRide = true;
                SetStatus($"RUNNING: {_scenarioTitle}; ждём Ride.");
            }
        }

        private void TickTimeoutCheck()
        {
            if (_attack.IsActive)
            {
                _timeoutWaitingStayedTrue &= _attack.IsWaitingForFirstJump;
                _timeoutBudgetStayedUntouched &=
                    _attack.JumpsRemaining == _timeoutInitialBudget;
                if (!_timeoutWaitingStayedTrue || !_timeoutBudgetStayedUntouched)
                {
                    SetChecklistResult(
                        1,
                        _timeoutWaitingStayedTrue,
                        "first-jump waiting");
                    SetChecklistResult(
                        2,
                        _timeoutBudgetStayedUntouched,
                        "jump budget");
                    SetChecklistResult(5, false, "jump input detected");
                    FailActiveCheck("Получен jump input или расходован jump budget.");
                }
                return;
            }

            float gameplaySeconds = Time.time - _timeoutStartedAt;
            bool waitedFullTimeout =
                gameplaySeconds >= SkateboardAttack.DefaultFirstJumpTimeout -
                _timeoutTolerance;
            bool normalActorRestored =
                !_hamster.ActorSwitcher.IsSkateboardActive &&
                _hamster.ActorSwitcher.NormalActor.activeSelf;
            SetChecklistResult(
                1,
                _timeoutWaitingStayedTrue,
                "first-jump waiting");
            SetChecklistResult(
                2,
                _timeoutBudgetStayedUntouched,
                "jump budget");
            SetChecklistResult(
                3,
                !_hamster.ActorSwitcher.IsSkateboardActive,
                "Skateboard actor");
            SetChecklistResult(
                4,
                _hamster.ActorSwitcher.NormalActor.activeSelf,
                "normal actor");
            bool timeoutPassed =
                waitedFullTimeout &&
                _timeoutWaitingStayedTrue &&
                _timeoutBudgetStayedUntouched &&
                normalActorRestored;
            SetChecklistResult(
                5,
                timeoutPassed,
                $"{gameplaySeconds:F2} gameplay s");
            if (!timeoutPassed)
            {
                FailActiveCheck(
                    $"elapsed={gameplaySeconds:F2}, waiting={_timeoutWaitingStayedTrue}, " +
                    $"budget={_timeoutBudgetStayedUntouched}, normal={normalActorRestored}.");
                return;
            }

            PassActiveCheck(
                "First-jump waiting сохранился, budget не расходован, " +
                "Skateboard выключен, normal actor включён.");
        }

        private void TickLaneShiftCheck()
        {
            if (_laneStage == LaneCheckStage.WaitRideShiftCompletion &&
                !_hamster.IsShifting.Value)
            {
                bool passed =
                    _hamster.IsOnBottomLine.Value == _laneTarget &&
                    _laneTarget != _laneStart;
                SetChecklistResult(
                    0,
                    passed,
                    passed ? "shift завершён на другой линии" : "линия не изменилась");
                _laneStage = LaneCheckStage.AwaitShiftTap;
                _instruction =
                    "В Ride начните новый shift и нажмите Jump, пока shift ещё идёт.";
                SetStatus($"RUNNING: {_scenarioTitle}. {_instruction}");
            }

            if (_laneStage == LaneCheckStage.WaitShiftJumpRecovery &&
                !_hamster.IsShifting.Value &&
                (!_attack.IsActive || _attack.IsRiding))
            {
                _laneStage = LaneCheckStage.AwaitJumpForRejectedTap;
                _instruction =
                    "В Ride начните Jump без shift, затем смените линию во время jump.";
                SetStatus($"RUNNING: {_scenarioTitle}. {_instruction}");
            }

            _lastObservedLane = _hamster.IsOnBottomLine.Value;
        }

        private void OnGuidedTapRequested()
        {
            if (_mode != RunnerMode.LaneShift ||
                _gameManager.State != GameState.PLAYING) return;

            bool laneChanged = _hamster.IsOnBottomLine.Value != _lastObservedLane;
            bool shiftStarted = _hamster.IsShifting.Value && laneChanged;
            if (_laneStage == LaneCheckStage.AwaitRideTap)
            {
                if (!_attack.IsActive || !_attack.IsRiding || !shiftStarted) return;
                _laneStart = _lastObservedLane;
                _laneTarget = _hamster.IsOnBottomLine.Value;
                _laneStage = LaneCheckStage.WaitRideShiftCompletion;
                _instruction = "Дождитесь завершения текущего shift.";
                SetStatus($"RUNNING: {_scenarioTitle}. Shift начат.");
                return;
            }

            if (_laneStage == LaneCheckStage.AwaitShiftTap)
            {
                if (!_attack.IsActive || !_attack.IsRiding || !shiftStarted) return;
                _laneStage = LaneCheckStage.AwaitJumpDuringShift;
                _instruction = "Нажмите Jump до завершения shift.";
                SetStatus($"RUNNING: {_scenarioTitle}. {_instruction}");
                return;
            }

            if (_laneStage != LaneCheckStage.AwaitTapDuringJump) return;
            bool rejected =
                !laneChanged && !_hamster.IsShifting.Value &&
                (_attack.IsJumping || _attack.IsLanding);
            SetChecklistResult(
                2,
                rejected,
                rejected ? "lane не изменилась" : "tap запустил shift во время jump");
            if (AllChecklistItemsPassed())
                PassActiveCheck("Skateboard повторяет normal lane-shift input contract.");
            else if (!rejected)
                SetStatus("FAIL: Lane Shift. Tap после jump был принят.", isError: true);
        }

        private void OnGuidedJumpRequested()
        {
            if (_mode != RunnerMode.LaneShift ||
                _gameManager.State != GameState.PLAYING) return;

            if (_laneStage == LaneCheckStage.AwaitJumpDuringShift)
            {
                bool accepted =
                    _hamster.IsShifting.Value && _attack.IsActive && _attack.IsJumping;
                SetChecklistResult(
                    1,
                    accepted,
                    accepted ? "jump принят во время shift" : "jump отклонён");
                _laneStage = LaneCheckStage.WaitShiftJumpRecovery;
                _instruction = "Дождитесь возврата в Ride.";
                SetStatus(accepted
                        ? $"RUNNING: {_scenarioTitle}. {_instruction}"
                        : "FAIL: Lane Shift. Jump во время shift отклонён.",
                    isError: !accepted);
                return;
            }

            if (_laneStage == LaneCheckStage.AwaitJumpForRejectedTap &&
                !_hamster.IsShifting.Value && _attack.IsActive && _attack.IsJumping)
            {
                _laneStage = LaneCheckStage.AwaitTapDuringJump;
                _instruction = "Сейчас смените линию до возврата в Ride.";
                SetStatus($"RUNNING: {_scenarioTitle}. {_instruction}");
            }
        }

        private void StartCollisionCheck(
            RunnerMode mode,
            string title,
            string instruction)
        {
            if (!TryStartCheckWithMode(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                return;
            }

            _hamster = hamster;
            _attack = attack;
            _gameManager = gameManager;
            _mode = mode;
            _scenarioTitle = title;
            _instruction = instruction;
            SetChecklist(_physicalLabels);
            CollisionController.SkateboardCollisionProcessed +=
                OnSkateboardCollisionProcessed;
            SetStatus($"PASS: Enter Mode. RUNNING: {title}. {instruction}");
        }

        private void OnSkateboardCollisionProcessed(
            CollisionController.SkateboardCollisionDiagnostic diagnostic)
        {
            if (diagnostic.Hamster != _hamster ||
                (_mode != RunnerMode.RideDamage &&
                 _mode != RunnerMode.JumpCollision)) return;

            if (diagnostic.Outcome ==
                CollisionController.SkateboardCollisionOutcome.Support)
            {
                SetStatus(
                    $"RUNNING: {_scenarioTitle}. Roof top/support пропущен; " +
                    "нужен side/road contact.");
                return;
            }

            if (diagnostic.Outcome ==
                CollisionController.SkateboardCollisionOutcome.Collect)
            {
                SetStatus($"RUNNING: {_scenarioTitle}. Collectible не входит в checklist.");
                return;
            }

            int index = FindPhysicalTypeIndex(diagnostic.ObstacleType);
            if (index < 0 || _checklist[index].State == ChecklistState.Pass) return;

            bool passed;
            string details;
            if (_mode == RunnerMode.RideDamage)
            {
                bool lifeLostExactlyOnce =
                    diagnostic.LivesAfter == diagnostic.LivesBefore - 1;
                bool modeCompleted = !_attack.IsActive;
                bool normalActorActive =
                    !_hamster.ActorSwitcher.IsSkateboardActive &&
                    _hamster.ActorSwitcher.NormalActor.activeSelf;
                passed =
                    diagnostic.Outcome ==
                    CollisionController.SkateboardCollisionOutcome.Damage &&
                    lifeLostExactlyOnce && modeCompleted && normalActorActive &&
                    diagnostic.ObstacleActiveAfter;
                details = passed
                    ? "life -1, mode завершён, normal actor, obstacle сохранён"
                    : $"outcome={diagnostic.Outcome}, lives=" +
                      $"{diagnostic.LivesBefore}->{diagnostic.LivesAfter}, " +
                      $"modeOff={modeCompleted}, normal={normalActorActive}, " +
                      $"obstacleAlive={diagnostic.ObstacleActiveAfter}";
            }
            else
            {
                bool lifeUnchanged = diagnostic.LivesAfter == diagnostic.LivesBefore;
                passed =
                    diagnostic.Outcome ==
                    CollisionController.SkateboardCollisionOutcome.Destroy &&
                    diagnostic.WasJumpCollisionActive && lifeUnchanged &&
                    !diagnostic.ObstacleActiveAfter &&
                    diagnostic.RoofSupportActiveAfter;
                details = passed
                    ? "jump phase, no damage, obstacle destroyed, support сохранён"
                    : $"outcome={diagnostic.Outcome}, " +
                      $"jumpPhase={diagnostic.WasJumpCollisionActive}, " +
                      $"lives={diagnostic.LivesBefore}->{diagnostic.LivesAfter}, " +
                      $"obstacleAlive={diagnostic.ObstacleActiveAfter}, " +
                      $"supportAlive={diagnostic.RoofSupportActiveAfter}";
            }

            SetChecklistResult(index, passed, details);
            if (!passed)
            {
                SetStatus(
                    $"FAIL: {_scenarioTitle}; {_physicalLabels[index]}. {details}",
                    isError: true);
                return;
            }

            if (AllChecklistItemsPassed())
            {
                PassActiveCheck("Все 6 physical obstacle types прошли.");
                return;
            }

            _instruction = _mode == RunnerMode.RideDamage
                ? "После damage recovery runner сам вернёт Skateboard."
                : "Продолжайте: active jump должен попасть в pending side/road type.";
            SetStatus($"PASS: {_physicalLabels[index]}. {_instruction}");
        }

        private bool RequestJumpCycle()
        {
            int beforeBudget = _attack.JumpsRemaining;
            _hamster.JumpRequest.Invoke();
            bool accepted = _attack.IsJumping ||
                            (_attack.IsLanding &&
                             _attack.JumpsRemaining == beforeBudget);
            if (!accepted) return false;
            if (_useSuperJump) _hamster.SuperJumpRequest.Invoke();
            return true;
        }

        private void ProbeRejectedActivation(Hamster hamster, SkateboardAttack attack)
        {
            int previousCharge = hamster.UltaChargeAmount.Value;
            int jumpsBefore = attack.JumpsRemaining;
            int comboBefore = attack.ComboDepth;
            bool wasRiding = attack.IsRiding;
            bool wasJumping = attack.IsJumping;
            bool wasLanding = attack.IsLanding;
            hamster.UltaChargeAmount.Value = 100;
            hamster.UltaEvent.Invoke();
            hamster.UltaChargeAmount.Value = previousCharge;

            bool unchanged = attack.IsActive &&
                             attack.JumpsRemaining == jumpsBefore &&
                             attack.ComboDepth == comboBefore &&
                             attack.IsRiding == wasRiding &&
                             attack.IsJumping == wasJumping &&
                             attack.IsLanding == wasLanding;
            SetStatus(unchanged
                    ? "PASS: repeated activation отклонена; active mode не сброшен."
                    : "FAIL: repeated activation изменила active Skateboard state.",
                isError: !unchanged);
        }

        private static bool TryEnterMode(
            Hamster hamster,
            SkateboardAttack attack,
            out string error)
        {
            if (attack.IsActive)
            {
                error = "Skateboard уже активен.";
                return false;
            }

            hamster.UltaChargeAmount.Value = 100;
            hamster.UltaEvent.Invoke();
            if (!attack.IsActive)
            {
                error = "UltaEvent не активировал Skateboard. Нужен stable Run/RoofRun.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnLandingImpact(int comboDepth, bool isSuperCycle)
        {
            if (_mode == RunnerMode.Timeout)
            {
                SetChecklistResult(1, false, "jump started");
                SetChecklistResult(2, false, "budget spent");
                SetChecklistResult(5, false, "jump input detected");
                FailActiveCheck("Timeout получил jump/landing impact.");
                return;
            }
            if (_mode != RunnerMode.Combo) return;

            _observedImpactDepths.Add(comboDepth);
            SetStatus(
                $"RUNNING: {_scenarioTitle}; impact combo={comboDepth}, " +
                $"cycle={(isSuperCycle ? "Super" : "Normal")}.");
        }

        private void ValidateAndPassComboScenario()
        {
            bool normalActorRestored =
                !_hamster.ActorSwitcher.IsSkateboardActive &&
                _hamster.ActorSwitcher.NormalActor.activeSelf;
            if (!normalActorRestored)
            {
                FailActiveCheck("Natural completion не восстановил normal actor.");
                return;
            }

            if (_observedImpactDepths.Count != _expectedImpactDepths.Length)
            {
                FailActiveCheck(
                    $"Impact count {_observedImpactDepths.Count}, " +
                    $"expected {_expectedImpactDepths.Length}.");
                return;
            }

            for (int index = 0; index < _expectedImpactDepths.Length; index++)
            {
                if (_observedImpactDepths[index] == _expectedImpactDepths[index]) continue;
                FailActiveCheck(
                    $"Impact #{index + 1}: {_observedImpactDepths[index]}, " +
                    $"expected {_expectedImpactDepths[index]}.");
                return;
            }

            PassActiveCheck(
                $"Impacts [{string.Join(",", _observedImpactDepths)}], " +
                "budget exhausted, normal actor restored.");
        }

        private bool ValidateScriptedSurface(Hamster hamster, out string instruction)
        {
            bool commonStable =
                !hamster.IsShifting.Value && !hamster.IsDamaged.Value &&
                !hamster.ActorSwitcher.IsSkateboardActive;
            if (!_onRoof)
            {
                bool stableRoad =
                    commonStable && hamster.HamsterState.Value == HamsterStateEnum.Run;
                instruction = stableRoad
                    ? string.Empty
                    : "Перейдите на дорогу, дождитесь stable Run и нажмите снова.";
                return stableRoad;
            }

            Obstacle roof = hamster.LastObstacle.Value;
            bool stableRoof =
                commonStable && hamster.HamsterState.Value == HamsterStateEnum.RoofRun &&
                roof != null && roof.isActiveAndEnabled && roof.ObstacleType != null &&
                (roof.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.bigNotAlive ||
                 roof.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive);
            instruction = stableRoof
                ? string.Empty
                : "Перейдите на крышу, дождитесь stable RoofRun и нажмите снова.";
            return stableRoof;
        }

        private bool TryStartCheckWithMode(
            out Hamster hamster,
            out SkateboardAttack attack,
            out GameManager gameManager)
        {
            if (!TryStartGuidedCheck(out hamster, out attack, out gameManager))
                return false;
            if (attack.IsActive)
            {
                SetStatus("Сначала завершите текущий Skateboard mode.");
                return false;
            }
            if (!TryEnterMode(hamster, attack, out string error))
            {
                SetStatus($"FAIL: {error}", isError: true);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Повторно активирует mode для следующего шага active guided check.
        /// </summary>
        private void TickGuidedModeLease()
        {
            if (_attack.IsActive ||
                _hamster.IsDamaged.Value ||
                _hamster.IsShifting.Value ||
                (_hamster.HamsterState.Value != HamsterStateEnum.Run &&
                 _hamster.HamsterState.Value != HamsterStateEnum.RoofRun))
            {
                return;
            }

            if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun &&
                (_hamster.LastObstacle.Value == null ||
                 !_hamster.LastObstacle.Value.isActiveAndEnabled))
            {
                return;
            }

            if (!TryEnterMode(_hamster, _attack, out string error))
            {
                FailActiveCheck($"Повторный Enter Mode отклонён: {error}");
                return;
            }

            SetStatus($"PASS: Enter Mode. RUNNING: {_scenarioTitle}. {_instruction}");
        }

        private bool TryStartGuidedCheck(
            out Hamster hamster,
            out SkateboardAttack attack,
            out GameManager gameManager)
        {
            if (!TryResolvePlayingGameplay(out hamster, out attack, out gameManager))
            {
                SetStatus(GetGameplayUnavailableStatus());
                return false;
            }
            if (IsBusy)
            {
                SetStatus($"Сначала завершите active check: {_scenarioTitle}.");
                return false;
            }
            _checklist.Clear();
            return true;
        }

        private void PassActiveCheck(string details)
        {
            string title = _scenarioTitle;
            SkateboardAttack attack = _attack;
            StopActiveCheck(clearChecklist: false);
            if (attack?.IsActive == true)
                attack.Complete();
            SetStatus($"PASS: {title}. {details}");
        }

        private void FailActiveCheck(string details)
        {
            string title = _scenarioTitle;
            SkateboardAttack attack = _attack;
            StopActiveCheck(clearChecklist: false);
            if (attack?.IsActive == true)
                attack.Complete();
            SetStatus($"FAIL: {title}. {details}", isError: true);
        }

        private void StopActiveCheck(bool clearChecklist)
        {
            if (_attack != null) _attack.LandingImpact -= OnLandingImpact;
            if (_hamster != null)
            {
                _hamster.TapRequest.Unsubscribe(OnGuidedTapRequested);
                _hamster.JumpRequest.Unsubscribe(OnGuidedJumpRequested);
            }
            CollisionController.SkateboardCollisionProcessed -=
                OnSkateboardCollisionProcessed;

            _hamster = null;
            _attack = null;
            _gameManager = null;
            _mode = RunnerMode.None;
            _laneStage = LaneCheckStage.None;
            _comboGroups = Array.Empty<int>();
            _expectedImpactDepths = Array.Empty<int>();
            _observedImpactDepths.Clear();
            _groupIndex = 0;
            _cycleInGroup = 0;
            _cyclesScheduled = 0;
            _waitingForQueuedCycle = false;
            _waitingForRide = false;
            _waitingForCompletion = false;
            _landingHandled = false;
            _scenarioTitle = string.Empty;
            _instruction = string.Empty;
            if (clearChecklist) _checklist.Clear();
        }

        private void SetChecklist(IReadOnlyList<string> labels)
        {
            _checklist.Clear();
            for (int index = 0; index < labels.Count; index++)
                _checklist.Add(new ChecklistItem(labels[index], ChecklistState.Pending));
        }

        private void SetChecklistResult(int index, bool passed, string details)
        {
            _checklist[index] = new ChecklistItem(
                _checklist[index].Label,
                passed ? ChecklistState.Pass : ChecklistState.Fail,
                details);
            Changed?.Invoke();
        }

        private bool AllChecklistItemsPassed()
        {
            if (_checklist.Count == 0) return false;
            for (int index = 0; index < _checklist.Count; index++)
            {
                if (_checklist[index].State != ChecklistState.Pass) return false;
            }
            return true;
        }

        private static int FindPhysicalTypeIndex(ObstacleTypeEnum type)
        {
            for (int index = 0; index < _physicalTypes.Length; index++)
            {
                if (_physicalTypes[index] == type) return index;
            }
            return -1;
        }

        private static int CalculateMissingExperience(PlayerData playerData, int requiredLevel)
        {
            if (playerData.PlayerLevel >= requiredLevel) return 0;
            int levelsMissing = checked(requiredLevel - playerData.PlayerLevel);
            return checked(
                levelsMissing * PlayerExperienceService.PlayerLevelThreshold -
                playerData.ExperiencePoints);
        }

        private string BuildLiveStatus()
        {
            PlayerData playerData = GameDataManager.PlayerData;
            string player = playerData == null
                ? "PlayerData: unavailable"
                : $"Player: level={playerData.PlayerLevel}, xp={playerData.ExperiencePoints}, " +
                  $"selected={SuperAttackService.ActiveSuperAttackId?.ToString() ?? "none"}";
            if (!TryResolveGameplay(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                return player +
                       "\nGameplay: unavailable; enter level after selecting Skateboard.";
            }

            string phase = !attack.IsActive
                ? "Inactive"
                : attack.IsRiding
                    ? "Ride"
                    : attack.IsLanding
                        ? "Landing"
                        : attack.IsSuperJumping
                            ? "SuperJump"
                            : attack.IsJumping ? "Jump" : "Unknown";
            return player + "\n" +
                   $"Game={gameManager.State}, Hamster={hamster.HamsterState.Value}, " +
                   $"lane={(hamster.IsOnBottomLine.Value ? "Bottom" : "Top")}, " +
                   $"shifting={hamster.IsShifting.Value}\n" +
                   $"Mode={phase}, actor=" +
                   $"{(hamster.ActorSwitcher.IsSkateboardActive ? "Skateboard" : "Normal")}, " +
                   $"surface={hamster.SkateboardSurfaceController.State}\n" +
                   $"jumps={attack.JumpsRemaining}, combo={attack.ComboDepth}, " +
                   $"firstJumpWaiting={attack.IsWaitingForFirstJump}, " +
                   $"charge={hamster.UltaChargeAmount.Value}, lives={hamster.Lives.Value}";
        }

        private static bool TryResolvePlayingGameplay(
            out Hamster hamster,
            out SkateboardAttack attack,
            out GameManager gameManager)
        {
            return TryResolveGameplay(out hamster, out attack, out gameManager) &&
                   gameManager.State == GameState.PLAYING;
        }

        private static bool TryResolveGameplay(
            out Hamster hamster,
            out SkateboardAttack attack,
            out GameManager gameManager)
        {
            hamster = null;
            attack = null;
            gameManager = null;
            if (!Application.isPlaying || !TryFindHamster(out hamster)) return false;
            attack = hamster.SkateboardAttackRuntimeForTesting;
            gameManager = LevelController.Instance?.LevelData?.GameManager;
            return attack != null && gameManager != null;
        }

        private static bool TryFindHamster(out Hamster hamster)
        {
            hamster = LevelController.Instance?.LevelData?.Hamster;
            if (hamster != null) return true;
            hamster = UnityEngine.Object.FindAnyObjectByType<Hamster>(
                FindObjectsInactive.Include);
            return hamster != null;
        }

        private static bool TryGetGameManager(out GameManager gameManager)
        {
            gameManager = LevelController.Instance?.LevelData?.GameManager;
            return Application.isPlaying && gameManager != null;
        }

        private static string GetGameplayUnavailableStatus()
        {
            return "Gameplay Skateboard runtime недоступен. " +
                   "Выберите Skateboard в Menu и войдите в новый уровень.";
        }

        private void SetStatus(string status, bool isError = false)
        {
            _status = status;
            if (isError) Debug.Log($"[Skateboard Testing][FAIL] {status}");
            Changed?.Invoke();
        }

        private enum RunnerMode
        {
            None,
            Combo,
            Timeout,
            RideDamage,
            JumpCollision,
            LaneShift,
        }

        private enum LaneCheckStage
        {
            None,
            AwaitRideTap,
            WaitRideShiftCompletion,
            AwaitShiftTap,
            AwaitJumpDuringShift,
            WaitShiftJumpRecovery,
            AwaitJumpForRejectedTap,
            AwaitTapDuringJump,
        }
    }
}
#endif
