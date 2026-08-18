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

        private readonly List<ChecklistItem> _checklist = new(6);

        private Hamster _hamster;
        private SkateboardAttack _attack;
        private GameManager _gameManager;
        private RunnerMode _mode;
        private LaneCheckStage _laneStage;
        private int _scriptedInitialBudget;
        private float _timeoutStartedAt;
        private int _timeoutInitialBudget;
        private bool _timeoutWaitingStayedTrue;
        private bool _timeoutBudgetStayedUntouched;
        private bool _scriptedUsesSuperJump;
        private bool _scriptedImpactObserved;
        private bool _scriptedImpactWasSuper;
        private ScriptedSurface _scriptedSurface;
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
            !IsBusy &&
            TryResolvePlayingGameplay(
                out Hamster hamster,
                out _,
                out _) &&
            TryDetectScriptedSurface(hamster, out _);
        public bool CanStartGuidedCheck =>
            !IsBusy && TryResolvePlayingGameplay(out _, out _, out _);
        public bool CanTogglePause =>
            TryGetGameManager(out GameManager manager) &&
            (manager.State == GameState.PLAYING ||
             IsPausedByTool && manager.State == GameState.PAUSED);
        public bool CanStopCheck =>
            IsBusy ||
            (TryResolveGameplay(out _, out SkateboardAttack attack, out _) &&
             attack.IsActive);

        /// <summary>
        /// Открывает Skateboard через development owner и выбирает его.
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
                if (!SuperAttackService.TryGet(_skateboardId, out _))
                    throw new InvalidOperationException("Skateboard ID 3 отсутствует в каталоге.");

                CharacterDevelopmentService.UnlockSuperAttackForTesting(
                    _skateboardId);
                if (!SuperAttackService.IsUnlocked(_skateboardId))
                    throw new InvalidOperationException("Skateboard не открылся через development owner.");
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
                    $"PASS: Skateboard открыт и выбран. " +
                    $"Development Points: {playerData.DevelopmentPoints}." +
                    runtimeNote);
            }
            catch (Exception exception)
            {
                SetStatus($"FAIL: {exception.Message}", isError: true);
            }
        }

        /// <summary>
        /// Запускает timeout check, не перехватывая ручной jump input.
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
            _instruction =
                "Для automatic timeout ничего не нажимайте. " +
                "Ручные Jump и Super Jump разрешены и остановят только check.";
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
            SetStatus($"PASS: Enter Mode. RUNNING: {_scenarioTitle}. {_instruction}");
        }

        public void RunJumpScenario() => StartScriptedScenario(useSuperJump: false);
        public void RunSuperJumpScenario() => StartScriptedScenario(useSuperJump: true);

        public void StartRideCollisionCheck() => StartCollisionCheck(
            RunnerMode.RideCollision,
            "Ride Collision",
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
                case RunnerMode.Scripted: TickScriptedScenario(); break;
                case RunnerMode.Timeout: TickTimeoutCheck(); break;
                case RunnerMode.RideCollision:
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

        private void StartScriptedScenario(bool useSuperJump)
        {
            if (!TryResolvePlayingGameplay(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                SetStatus(GetGameplayUnavailableStatus());
                return;
            }

            if (IsBusy || attack.IsActive)
            {
                SetStatus("Сначала завершите active check и Skateboard mode.");
                return;
            }

            if (!TryDetectScriptedSurface(
                    hamster,
                    out ScriptedSurface scriptedSurface))
            {
                SetStatus(
                    "Scripted scenario требует stable Run или " +
                    "stable RoofRun с живой support.");
                return;
            }

            if (!TryEnterMode(hamster, attack, out string error))
            {
                SetStatus($"FAIL: {error}", isError: true);
                return;
            }

            if (!DidEnterScriptedSurface(hamster, scriptedSurface))
            {
                attack.Complete();
                SetStatus(
                    $"FAIL: Enter Mode не сохранил " +
                    $"surface={GetScriptedSurfaceLabel(scriptedSurface)}.",
                    isError: true);
                return;
            }

            _hamster = hamster;
            _attack = attack;
            _gameManager = gameManager;
            _mode = RunnerMode.Scripted;
            _scriptedUsesSuperJump = useSuperJump;
            _scriptedInitialBudget = attack.JumpsRemaining;
            _scriptedImpactObserved = false;
            _scriptedImpactWasSuper = false;
            _scriptedSurface = scriptedSurface;
            string surfaceLabel = GetScriptedSurfaceLabel(_scriptedSurface);
            string actionLabel = useSuperJump ? "Super Jump" : "Jump";
            _scenarioTitle = $"{actionLabel}; surface={surfaceLabel}";
            _instruction = "Runner отправляет action request и ждёт landing/ride.";
            SetChecklist(new[]
            {
                $"Start surface={surfaceLabel}",
                $"{actionLabel} request accepted",
                $"Landing impact: {actionLabel}",
                "Jump budget decreased by 1",
                "Returned to Ride",
            });
            SetChecklistResult(
                0,
                true,
                scriptedSurface == ScriptedSurface.Road
                    ? "stable Run"
                    : "stable RoofRun; live support");
            attack.LandingImpact += OnLandingImpact;
            bool accepted = RequestScriptedAction();
            SetChecklistResult(
                1,
                accepted,
                accepted ? "runtime accepted" : "runtime rejected");
            if (!accepted)
            {
                FailActiveCheck($"{actionLabel} request отклонён runtime.");
                return;
            }

            SetStatus($"RUNNING: {_scenarioTitle}. Ждём landing impact.");
        }

        private void TickScriptedScenario()
        {
            if (!_attack.IsActive)
            {
                FailActiveCheck("Skateboard mode завершился до Ride.");
                return;
            }

            if (!_scriptedImpactObserved)
                return;

            bool expectedImpact =
                _scriptedImpactWasSuper == _scriptedUsesSuperJump;
            SetChecklistResult(
                2,
                expectedImpact,
                $"action={(_scriptedImpactWasSuper ? "Super Jump" : "Jump")}");
            if (!expectedImpact)
            {
                FailActiveCheck("Landing impact не совпал с выбранным action.");
                return;
            }

            if (!_attack.IsRiding)
                return;

            bool budgetPassed =
                _attack.JumpsRemaining == _scriptedInitialBudget - 1;
            SetChecklistResult(
                3,
                budgetPassed,
                $"{_scriptedInitialBudget}->{_attack.JumpsRemaining}");
            SetChecklistResult(4, true, "Ride");
            if (!budgetPassed)
            {
                FailActiveCheck("Jump budget изменился не на 1.");
                return;
            }

            PassActiveCheck("Один action cycle прошёл; Skateboard остановлен.");
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
                    ReleaseTimeoutCheckForManualJump();
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
            SkateboardCollisionDiagnostic diagnostic)
        {
            if (diagnostic.Hamster != _hamster ||
                (_mode != RunnerMode.RideCollision &&
                 _mode != RunnerMode.JumpCollision)) return;

            bool expectsStartedOnRoofPreserve =
                _mode == RunnerMode.JumpCollision &&
                diagnostic.WasJumpCollisionActive &&
                SkateboardInteractionPolicy.IsRoof(diagnostic.ObstacleType) &&
                _attack.TryGetCurrentJumpSnapshot(
                    out SkateboardJumpCycleSnapshot snapshot) &&
                snapshot.StartedOnRoof;
            if (diagnostic.Outcome ==
                    SkateboardCollisionOutcome.Support &&
                !expectsStartedOnRoofPreserve)
            {
                SetStatus(
                    $"RUNNING: {_scenarioTitle}. Roof top/support пропущен; " +
                    "нужен side/road contact.");
                return;
            }

            if (diagnostic.Outcome ==
                SkateboardCollisionOutcome.Collect)
            {
                SetStatus($"RUNNING: {_scenarioTitle}. Collectible не входит в checklist.");
                return;
            }

            int index = FindPhysicalTypeIndex(diagnostic.ObstacleType);
            if (index < 0 || _checklist[index].State == ChecklistState.Pass) return;

            bool passed;
            string details;
            if (_mode == RunnerMode.RideCollision)
            {
                bool lifeUnchanged =
                    diagnostic.LivesAfter == diagnostic.LivesBefore;
                bool modeStayedActive =
                    _attack.IsActive && _hamster.ActorSwitcher.IsSkateboardActive;
                bool expectsDestroy =
                    SkateboardInteractionPolicy.IsRoof(diagnostic.ObstacleType);
                bool obstacleOutcomeMatches = expectsDestroy
                    ? diagnostic.Outcome ==
                      SkateboardCollisionOutcome.Destroy &&
                      !diagnostic.ObstacleActiveAfter
                    : diagnostic.Outcome ==
                      SkateboardCollisionOutcome.Ignored &&
                      diagnostic.ObstacleActiveAfter;
                passed = lifeUnchanged && modeStayedActive && obstacleOutcomeMatches;
                details = passed
                    ? expectsDestroy
                        ? "no damage, mode active, medium/big destroyed"
                        : "no damage, mode active, obstacle preserved"
                    : $"outcome={diagnostic.Outcome}, lives=" +
                      $"{diagnostic.LivesBefore}->{diagnostic.LivesAfter}, " +
                      $"modeActive={modeStayedActive}, expectedDestroy={expectsDestroy}, " +
                      $"obstacleAlive={diagnostic.ObstacleActiveAfter}";
            }
            else
            {
                bool lifeUnchanged = diagnostic.LivesAfter == diagnostic.LivesBefore;
                if (expectsStartedOnRoofPreserve)
                {
                    passed =
                        diagnostic.Outcome ==
                        SkateboardCollisionOutcome.Support &&
                        lifeUnchanged && diagnostic.ObstacleActiveAfter;
                    details = passed
                        ? "StartedOnRoof jump, roof contact сохранён"
                        : $"expected=Support, outcome={diagnostic.Outcome}, " +
                          $"lives={diagnostic.LivesBefore}->{diagnostic.LivesAfter}, " +
                          $"obstacleAlive={diagnostic.ObstacleActiveAfter}";
                }
                else
                {
                    passed =
                        diagnostic.Outcome ==
                        SkateboardCollisionOutcome.Destroy &&
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

            _instruction = _mode == RunnerMode.RideCollision
                ? "Продолжайте collision checks: Skateboard остаётся активен."
                : "Продолжайте: active jump должен попасть в pending side/road type.";
            SetStatus($"PASS: {_physicalLabels[index]}. {_instruction}");
        }

        private bool RequestScriptedAction()
        {
            if (_scriptedUsesSuperJump)
            {
                _hamster.SuperJumpRequest.Invoke();
                return _attack.IsSuperJumping;
            }

            _hamster.JumpRequest.Invoke();
            return _attack.IsJumping && !_attack.IsSuperJumping;
        }

        private void OnLandingImpact(bool isSuperCycle)
        {
            if (_mode != RunnerMode.Scripted)
                return;

            _scriptedImpactWasSuper = isSuperCycle;
            _scriptedImpactObserved = true;
            SetStatus(
                $"RUNNING: {_scenarioTitle}; landing=" +
                $"{(isSuperCycle ? "Super Jump" : "Jump")}.");
        }

        private static bool TryDetectScriptedSurface(
            Hamster hamster,
            out ScriptedSurface surface)
        {
            surface = default;
            bool commonStable =
                !hamster.IsShifting.Value && !hamster.IsDamaged.Value &&
                !hamster.ActorSwitcher.IsSkateboardActive;

            if (commonStable && hamster.HamsterState.Value == HamsterStateEnum.Run)
            {
                surface = ScriptedSurface.Road;
                return true;
            }

            Obstacle roof = hamster.LastObstacle.Value;
            if (commonStable &&
                hamster.HamsterState.Value == HamsterStateEnum.RoofRun &&
                IsLiveRoofSupport(roof))
            {
                surface = ScriptedSurface.Roof;
                return true;
            }

            return false;
        }

        private static bool DidEnterScriptedSurface(
            Hamster hamster,
            ScriptedSurface surface)
        {
            Obstacle currentRoof =
                hamster.SkateboardSurfaceController.CurrentRoof;
            return surface == ScriptedSurface.Road
                ? currentRoof == null
                : IsLiveRoofSupport(currentRoof);
        }

        private static bool IsLiveRoofSupport(Obstacle roof)
        {
            return roof != null &&
                   roof.isActiveAndEnabled &&
                   roof.ObstacleType != null &&
                   SkateboardInteractionPolicy.IsRoof(
                       roof.ObstacleType.ObstacleTypeEnum);
        }

        private static string GetScriptedSurfaceLabel(ScriptedSurface surface)
        {
            return surface == ScriptedSurface.Roof ? "Roof" : "Road";
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

        private void ReleaseTimeoutCheckForManualJump()
        {
            string jumpKind = _attack?.IsSuperJumping == true
                ? "Super Jump"
                : "Jump";
            StopActiveCheck(clearChecklist: true);
            SetStatus(
                $"MANUAL: {jumpKind} принят. Timeout check остановлен; " +
                "Skateboard gameplay продолжает работать.");
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
            if (_attack != null)
                _attack.LandingImpact -= OnLandingImpact;
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
            _scriptedInitialBudget = 0;
            _scriptedUsesSuperJump = false;
            _scriptedImpactObserved = false;
            _scriptedImpactWasSuper = false;
            _scriptedSurface = default;
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
                   $"jumps={attack.JumpsRemaining}, " +
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
            Scripted,
            Timeout,
            RideCollision,
            JumpCollision,
            LaneShift,
        }

        private enum ScriptedSurface
        {
            Road,
            Roof,
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
