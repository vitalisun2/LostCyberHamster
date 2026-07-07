using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Управляет последовательностью минимальных tutorial-уроков.
    /// </summary>
    public sealed class TutorialGameController
    {
        private enum TutorialState
        {
            Disabled,
            RunningToTrigger,
            WaitingForInput,
            ResolvingAction,
            Completed
        }

        private const float _automationActionDelaySeconds = 0.15f;
        private const float _automationUpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        private static readonly TutorialStepDefinition _superHitStep = new(
            number: 10,
            title: "Обучение 10 - суперудар",
            instruction: "Используйте суперудар",
            expectedAction: TutorialAction.Ultra,
            pauseDistance: 7.2f,
            targetTypes: new[]
            {
                ObstacleTypeEnum.smallNotAliveRoad,
                ObstacleTypeEnum.bigAlive,
                ObstacleTypeEnum.smallAlive
            });

        private readonly GameManager _gameManager;
        private readonly Hamster _hamster;
        private readonly TutorialGameplayOverlay _gameplayOverlay;
        private readonly IReadOnlyList<TutorialStepDefinition> _steps = new[]
        {
            new TutorialStepDefinition(
                number: 1,
                title: "Обучение 1 - уклониться",
                instruction: "Тапни, чтобы увернуться",
                expectedAction: TutorialAction.Tap,
                pauseDistance: 4.2f,
                targetTypes: ObstacleTypeEnum.smallNotAliveRoad),
            new TutorialStepDefinition(
                number: 2,
                title: "Обучение 2 - перепрыгнуть",
                instruction: "Нажми прыжок, чтобы перепрыгнуть",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 0.45f,
                targetTypes: ObstacleTypeEnum.smallNotAliveRoad),
            new TutorialStepDefinition(
                number: 3,
                title: "Обучение 3 - запрыгнуть",
                instruction: "Прыгни на препятствие",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 1.9f,
                targetTypes: ObstacleTypeEnum.smallAlive),
            new TutorialStepDefinition(
                number: 4,
                title: "Обучение 4 - суперпрыжок",
                instruction: "Нажми прыжок дважды, чтобы перелететь дальше",
                expectedActions: new[] { TutorialAction.Jump, TutorialAction.SuperJump },
                pauseDistance: 1.55f,
                targetTypes: ObstacleTypeEnum.bigAlive),
            new TutorialStepDefinition(
                number: 5,
                title: "Обучение 5 - супернапрыг",
                instruction: "Нажми прыжок дважды и приземлись сверху",
                expectedActions: new[] { TutorialAction.Jump, TutorialAction.SuperJump },
                pauseDistance: 3.2f,
                targetTypes: ObstacleTypeEnum.smallAlive),
            new TutorialStepDefinition(
                number: 6,
                title: "Обучение 6 - запрыгнуть на крышу",
                instruction: "Прыгни на крышу",
                expectedActions: new[] { TutorialAction.Jump },
                pauseDistance: 2.4f,
                completionState: HamsterStateEnum.RoofRun,
                targetTypes: new[] { ObstacleTypeEnum.bigNotAlive, ObstacleTypeEnum.mediumNotAlive }),
            new TutorialStepDefinition(
                number: 7,
                title: "Обучение 7 - крыша к крыше",
                instruction: "Прыгни на следующую крышу",
                expectedActions: new[] { TutorialAction.Jump },
                pauseDistance: 3.2f,
                completionState: HamsterStateEnum.RoofRun,
                targetTypes: new[] { ObstacleTypeEnum.bigNotAlive, ObstacleTypeEnum.mediumNotAlive }),
            new TutorialStepDefinition(
                number: 8,
                title: "Обучение 8 - напрыгнуть с крыши",
                instruction: "Прыгни с крыши на препятствие",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 3.9f,
                targetTypes: ObstacleTypeEnum.smallAlive)
        };

        private TutorialState _state = TutorialState.Disabled;
        private int _currentStepIndex;
        private int _currentActionIndex;
        private Obstacle _trackedObstacle;
        private bool _automationActionScheduled;
        private float _automationActionTime;
        private Action _automationCompletionAction;
        private bool _automationCompletionActionScheduled;
        private float _automationCompletionActionTime;
        private bool _isSuperHitLesson;
        private bool _superHitUsed;
        private bool _superHitEffectObserved;
        private bool _superHitResolutionLogged;
        private bool _isSubscribedToUltaUsed;
        private readonly List<Obstacle> _superHitTargets = new();

        private TutorialStepDefinition CurrentStep => _isSuperHitLesson ? _superHitStep : _steps[_currentStepIndex];
        private TutorialAction CurrentExpectedAction => CurrentStep.ExpectedActions[_currentActionIndex];

        public TutorialGameController(GameManager gameManager, Hamster hamster)
        {
            _gameManager = gameManager;
            _hamster = hamster;
            _gameplayOverlay = new TutorialGameplayOverlay();

            _gameplayOverlay.SetActions(StartFirstGameplayLevel, StartFirstGameplayLevel, OpenMenu);
            _gameplayOverlay.SetGameplayAction(HandleGameplayAction);
        }

        public void AttachGameplayRoot(VisualElement root)
        {
            _gameplayOverlay.Attach(root);
        }

        /// <summary>
        /// Включает tutorial UI только на tutorial level.
        /// </summary>
        public void InitializeIfNeeded()
        {
            if (!TutorialConstants.IsTutorialLevel(GameDataManager.PlayerData?.CurrentLevel))
            {
                _gameplayOverlay.Hide();
                _state = TutorialState.Disabled;
                return;
            }

            ResetLessonState();

            if (TutorialConstants.IsSuperHitTutorialLevel(GameDataManager.PlayerData?.CurrentLevel))
            {
                InitializeSuperHitLesson();
                return;
            }

            InitializeCoreLesson();
        }

        private void ResetLessonState()
        {
            UnsubscribeFromUltaUsed();
            _currentStepIndex = 0;
            _currentActionIndex = 0;
            _trackedObstacle = null;
            _automationActionScheduled = false;
            _automationCompletionAction = null;
            _automationCompletionActionScheduled = false;
            _isSuperHitLesson = false;
            _superHitUsed = false;
            _superHitEffectObserved = false;
            _superHitResolutionLogged = false;
            _superHitTargets.Clear();
            _state = TutorialState.RunningToTrigger;
        }

        private void InitializeCoreLesson()
        {
            TutorialMetaCoordinator.ResetForNewTutorialRun();
            TutorialSandboxState.PrepareCoreLesson();
            _gameplayOverlay.ShowHeader(CurrentStep.Title);
            LogTutorial($"started steps={_steps.Count}");
        }

        private void InitializeSuperHitLesson()
        {
            _isSuperHitLesson = true;
            TutorialSandboxState.PrepareSuperHitLesson(TutorialMetaCoordinator.ElectricStrikeSkinId);
            HelpMethods.ApplyOverrideController(_hamster);
            _hamster.AddUltaCharge(100);
            SubscribeToUltaUsed();
            _gameplayOverlay.ShowHeader(CurrentStep.Title);
            LogTutorial(
                $"super hit lesson started skin={SkinManager.CurrentSkin?.Id} " +
                $"charge={_hamster.UltaChargeAmount.Value}");
        }

        /// <summary>
        /// Обновляет текущую фазу tutorial-урока.
        /// </summary>
        public void OnUpdate()
        {
            if (_state == TutorialState.RunningToTrigger)
            {
                TryPauseAtTutorialObstacle();
                return;
            }

            if (_state == TutorialState.WaitingForInput)
            {
                TryPerformAutomationAction();
                return;
            }

            if (_state == TutorialState.ResolvingAction)
            {
                TryCompleteAfterObstacleLeavesScreen();
                return;
            }

            if (_state == TutorialState.Completed)
            {
                TryPerformAutomationCompletionAction();
            }
        }

        /// <summary>
        /// Обрабатывает действие игрока и сообщает, можно ли передать его в gameplay.
        /// </summary>
        public bool TryHandleInput(TutorialAction action)
        {
            if (_state == TutorialState.Disabled)
            {
                return true;
            }

            if (_state != TutorialState.WaitingForInput || action != CurrentExpectedAction)
            {
                return false;
            }

            _gameManager.Resume();
            LogTutorial($"input accepted step={CurrentStep.Number} action={action} state={_hamster.HamsterState.Value}");

            if (_currentActionIndex + 1 < CurrentStep.ExpectedActions.Count)
            {
                _currentActionIndex++;
                _gameplayOverlay.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
                return true;
            }

            _gameplayOverlay.HidePrompt();
            _state = TutorialState.ResolvingAction;
            return true;
        }

        private void HandleGameplayAction(TutorialAction action)
        {
            if (!TryHandleInput(action))
            {
                return;
            }

            ForwardGameplayAction(action);
        }

        private void TryPauseAtTutorialObstacle()
        {
            if (_gameManager.State != GameState.PLAYING || ObstacleSpawner.Instance == null)
            {
                return;
            }

            var obstacle = FindNextSameLineObstacle();
            if (obstacle == null)
            {
                return;
            }

            float distance = GetDistanceToHamster(obstacle);
            if (distance > CurrentStep.PauseDistance || distance < 0f)
            {
                return;
            }

            _trackedObstacle = obstacle;
            if (_isSuperHitLesson)
            {
                CaptureSuperHitTargets();
            }

            _currentActionIndex = 0;
            _state = TutorialState.WaitingForInput;
            _gameManager.Pause();
            LogTutorial(
                $"pause step={CurrentStep.Number} action={CurrentExpectedAction} " +
                $"target={obstacle.ObstacleType.ObstacleTypeEnum} distance={distance:0.00} " +
                $"state={_hamster.HamsterState.Value}");
            _gameplayOverlay.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
            ScheduleAutomationActionIfNeeded(_automationActionDelaySeconds);
        }

        private Obstacle FindNextSameLineObstacle()
        {
            return ObstacleSpawner.Instance.SpawnedObstacles
                .Select(spawned => spawned?.ObstacleScript)
                .Where(obstacle => obstacle != null)
                .Where(obstacle => HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
                .Where(IsExpectedTargetType)
                .Where(obstacle => obstacle.transform.position.x > _hamster.transform.position.x)
                .OrderBy(obstacle => obstacle.transform.position.x)
                .FirstOrDefault();
        }

        private bool IsExpectedTargetType(Obstacle obstacle)
        {
            return CurrentStep.TargetTypes.Count == 0
                   || CurrentStep.TargetTypes.Contains(obstacle.ObstacleType.ObstacleTypeEnum);
        }

        private float GetDistanceToHamster(Obstacle obstacle)
        {
            CollisionUtils.GetObstacleXInterval(
                obstacle,
                obstacle.ColliderWidth,
                0f,
                out float obstacleLeftX,
                out _);

            return obstacleLeftX - _hamster.RightX;
        }

        private void TryCompleteAfterObstacleLeavesScreen()
        {
            if (_isSuperHitLesson)
            {
                if (!IsSuperHitResolutionComplete())
                {
                    return;
                }

                AdvanceToNextStepOrComplete();
                return;
            }

            if (CurrentStep.CompletionState.HasValue
                && _hamster.HamsterState.Value == CurrentStep.CompletionState.Value)
            {
                AdvanceToNextStepOrComplete();
                return;
            }

            if (_trackedObstacle == null || IsTrackedObstacleOutOfPlay() || IsTrackedObstaclePastLeftScreenEdge())
            {
                AdvanceToNextStepOrComplete();
            }
        }

        private void CaptureSuperHitTargets()
        {
            _superHitTargets.Clear();
            _superHitTargets.AddRange(FindSuperHitTargetsInRange());
            _superHitEffectObserved = false;
            _superHitResolutionLogged = false;
            LogTutorial($"super hit targets captured count={_superHitTargets.Count}");
        }

        private IEnumerable<Obstacle> FindSuperHitTargetsInRange()
        {
            if (ObstacleSpawner.Instance == null)
            {
                return Enumerable.Empty<Obstacle>();
            }

            return ObstacleSpawner.Instance.SpawnedObstacles
                .Select(spawned => spawned?.ObstacleScript)
                .Where(obstacle => obstacle != null)
                .Where(obstacle => HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
                .Where(obstacle => obstacle.transform.position.x >= _hamster.transform.position.x)
                .Where(obstacle =>
                    Mathf.Abs(_hamster.transform.position.x - obstacle.transform.position.x) <= Consts.StrikeRangeMax)
                .ToList();
        }

        private bool IsSuperHitResolutionComplete()
        {
            if (!_superHitUsed)
            {
                return false;
            }

            if (IsElectricStrikeEffectPlaying())
            {
                _superHitEffectObserved = true;
                return false;
            }

            if (!_superHitEffectObserved || HasAnyCapturedSuperHitTargetInPlay())
            {
                return false;
            }

            if (!_superHitResolutionLogged)
            {
                _superHitResolutionLogged = true;
                LogTutorial($"super hit resolved capturedTargets={_superHitTargets.Count}");
            }

            return true;
        }

        private static bool IsElectricStrikeEffectPlaying()
        {
            return UnityEngine.Object.FindAnyObjectByType<global::ElectricStrikeUlta>(
                FindObjectsInactive.Include) != null;
        }

        private bool HasAnyCapturedSuperHitTargetInPlay()
        {
            if (_superHitTargets.Count == 0 || ObstacleSpawner.Instance == null)
            {
                return false;
            }

            return ObstacleSpawner.Instance.SpawnedObstacles
                .Select(spawned => spawned?.ObstacleScript)
                .Any(obstacle => obstacle != null && _superHitTargets.Contains(obstacle));
        }

        private bool IsTrackedObstacleOutOfPlay()
        {
            return ObstacleSpawner.Instance == null
                   || ObstacleSpawner.Instance.SpawnedObstacles.All(spawned => spawned?.ObstacleScript != _trackedObstacle);
        }

        private bool IsTrackedObstaclePastLeftScreenEdge()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            CollisionUtils.GetObstacleXInterval(
                _trackedObstacle,
                _trackedObstacle.ColliderWidth,
                0f,
                out _,
                out float obstacleRightX);

            float screenLeftEdge = mainCamera.transform.position.x - mainCamera.orthographicSize * mainCamera.aspect;
            return obstacleRightX < screenLeftEdge;
        }

        private void AdvanceToNextStepOrComplete()
        {
            _trackedObstacle = null;
            _currentActionIndex = 0;
            _automationActionScheduled = false;
            LogTutorial(
                $"step completed step={CurrentStep.Number} " +
                $"state={_hamster.HamsterState.Value} lives={_hamster.Lives.Value}" +
                GetDamageSuffix());

            if (ShouldStopAfterCurrentStepForAutomation())
            {
                StartSkinTutorial();
                return;
            }

            if (_isSuperHitLesson)
            {
                CompleteTutorial();
                return;
            }

            if (_currentStepIndex + 1 >= _steps.Count)
            {
                StartSkinTutorial();
                return;
            }

            _currentStepIndex++;
            _state = TutorialState.RunningToTrigger;
            _gameplayOverlay.ShowHeader(CurrentStep.Title);
        }

        private bool ShouldStopAfterCurrentStepForAutomation()
        {
            return !_isSuperHitLesson
                   && TutorialAutomationSettings.TryGetStopAfterStep(out var stopAfterStep)
                   && stopAfterStep == CurrentStep.Number;
        }

        private void StartSkinTutorial()
        {
            if (_state == TutorialState.Completed)
            {
                return;
            }

            _state = TutorialState.Completed;
            _gameManager.Pause();
            _gameplayOverlay.Hide();
            TutorialMetaCoordinator.BeginElectricStrikeSkinLesson();
            LogTutorial($"core controls completed lives={_hamster.Lives.Value}{GetDamageSuffix()}");
            _gameplayOverlay.SetActions(OpenMenuForSkinTutorial, OpenMenuForSkinTutorial, OpenMenuForSkinTutorial);
            _gameplayOverlay.ShowComplete(
                "Вы освоили управление",
                $"Попробуйте купить скин с молнией за {TutorialMetaCoordinator.RewardCrystals} учебных кристаллов.",
                "В меню",
                "В меню",
                false);
            ScheduleAutomationCompletionAction(OpenMenuForSkinTutorial, 0.75f);
        }

        private void CompleteTutorial()
        {
            if (_state == TutorialState.Completed)
            {
                return;
            }

            _state = TutorialState.Completed;
            UnsubscribeFromUltaUsed();
            _gameManager.Pause();
            LogTutorial($"completed lives={_hamster.Lives.Value}{GetDamageSuffix()}");
            if (TutorialAutomationSettings.ShouldAutoPlay())
            {
                DebugManager.DiagStability("[TEST RESULT] WIN Tutorial completed");
            }

            DeviceLogUploader.UploadDiagnosticLog("tutorial_completed");

            _gameplayOverlay.SetActions(StartFirstGameplayLevel, StartFirstGameplayLevel, OpenMenu);
            _gameplayOverlay.ShowComplete(
                "Поздравляю",
                "Вы прошли обучение");
        }

        private void StartFirstGameplayLevel()
        {
            TutorialMetaCoordinator.RestoreSandbox(markTutorialCompleted: true);
            TutorialLaunchState.AllowFirstGameplayLevelOnce();
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.FirstGameplayLevelAddress;
            GameDataManager.SaveData();
            SceneManager.LoadScene(TutorialConstants.GameSceneName);
        }

        private void OpenMenu()
        {
            TutorialMetaCoordinator.RestoreSandbox(markTutorialCompleted: true);
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.FirstGameplayLevelAddress;
            GameDataManager.SaveData();
            SceneManager.LoadScene(TutorialConstants.MenuSceneName);
        }

        private void OpenMenuForSkinTutorial()
        {
            _gameManager.Resume();
            Time.timeScale = 1f;
            LogTutorial($"opening menu for skin lesson timeScale={Time.timeScale}");
            GameDataManager.IsGameJustStarted = false;
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.FirstGameplayLevelAddress;
            SceneManager.LoadScene(TutorialConstants.MenuSceneName);
        }

        public void Dispose()
        {
            UnsubscribeFromUltaUsed();
            _gameplayOverlay.Dispose();
        }

        private void ScheduleAutomationActionIfNeeded(float delaySeconds)
        {
            if (!TutorialAutomationSettings.ShouldAutoPlay())
            {
                return;
            }

            _automationActionScheduled = true;
            _automationActionTime = Time.unscaledTime + delaySeconds;
        }

        private void ScheduleAutomationCompletionAction(Action action, float delaySeconds)
        {
            if (!TutorialAutomationSettings.ShouldAutoPlay() || action == null)
            {
                return;
            }

            _automationCompletionAction = action;
            _automationCompletionActionScheduled = true;
            _automationCompletionActionTime = Time.unscaledTime + delaySeconds;
        }

        private void TryPerformAutomationAction()
        {
            if (!_automationActionScheduled || Time.unscaledTime < _automationActionTime)
            {
                return;
            }

            _automationActionScheduled = false;
            var expectedAction = CurrentExpectedAction;

            if (!TryHandleInput(expectedAction))
            {
                return;
            }

            ForwardGameplayAction(expectedAction);

            if (_state == TutorialState.WaitingForInput)
            {
                ScheduleAutomationActionIfNeeded(_automationUpgradeDelaySeconds);
            }
        }

        private void TryPerformAutomationCompletionAction()
        {
            if (!_automationCompletionActionScheduled || Time.unscaledTime < _automationCompletionActionTime)
            {
                return;
            }

            _automationCompletionActionScheduled = false;
            var action = _automationCompletionAction;
            _automationCompletionAction = null;
            action?.Invoke();
        }

        private void ForwardGameplayAction(TutorialAction action)
        {
            switch (action)
            {
                case TutorialAction.Tap:
                    _hamster.TapRequest?.Invoke();
                    break;
                case TutorialAction.Jump:
                    if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                    {
                        _hamster.RoofJumpRequest?.Invoke();
                    }
                    else
                    {
                        _hamster.JumpRequest?.Invoke();
                    }
                    break;
                case TutorialAction.SuperJump:
                    if (_hamster.HamsterState.Value == HamsterStateEnum.RoofJump
                        || _hamster.HamsterState.Value == HamsterStateEnum.RoofJumpDamage
                        || _hamster.HamsterState.Value == HamsterStateEnum.JumpFromRoof
                        || _hamster.HamsterState.Value == HamsterStateEnum.JumpFromRoofDamage
                        || _hamster.HamsterState.Value == HamsterStateEnum.JumpOnObstacleFromRoof)
                    {
                        _hamster.SuperRoofJumpRequest?.Invoke();
                    }
                    else
                    {
                        _hamster.SuperJumpRequest?.Invoke();
                    }
                    break;
                case TutorialAction.Ultra:
                    _hamster.UltaEvent?.Invoke();
                    break;
            }
        }

        private void SubscribeToUltaUsed()
        {
            if (_isSubscribedToUltaUsed)
            {
                return;
            }

            GameEventsManager.OnUltaUsed += OnUltaUsed;
            _isSubscribedToUltaUsed = true;
        }

        private void UnsubscribeFromUltaUsed()
        {
            if (!_isSubscribedToUltaUsed)
            {
                return;
            }

            GameEventsManager.OnUltaUsed -= OnUltaUsed;
            _isSubscribedToUltaUsed = false;
        }

        private void OnUltaUsed()
        {
            if (!_isSuperHitLesson || _state != TutorialState.ResolvingAction)
            {
                return;
            }

            _superHitUsed = true;
            LogTutorial(
                $"super hit used charge={_hamster.UltaChargeAmount.Value} " +
                $"targets={_superHitTargets.Count}");
        }

        [Conditional("LCH_VERBOSE_TUTORIAL_DIAGNOSTICS")]
        private static void LogTutorial(string message)
        {
        }

        private string GetDamageSuffix()
        {
            return _hamster.IsDamaged.Value ? " DAMAGE=true" : string.Empty;
        }
    }
}
