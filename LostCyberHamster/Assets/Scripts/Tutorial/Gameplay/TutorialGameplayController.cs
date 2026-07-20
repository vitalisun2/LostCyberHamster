using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Управляет состоянием одного gameplay-сценария tutorial.
    /// </summary>
    public sealed class TutorialGameplayController : IDisposable
    {
        private enum TutorialGameplayState
        {
            RunningToTrigger,
            WaitingForInput,
            ResolvingAction,
            Completed,
            Disposed
        }

        private const float _automationActionDelaySeconds = 0.15f;
        private const float _automationUpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;
        private const float _automationCompletionDelaySeconds = 0.75f;

        private readonly GameManager _gameManager;
        private readonly Hamster _hamster;
        private readonly TutorialGameplayScenario _scenario;
        private readonly IReadOnlyList<TutorialGameplayStep> _steps;
        private readonly List<Obstacle> _superHitTargets = new();

        private TutorialGameplayView _view;
        private VisualElement _attachedRoot;
        private TutorialGameplayState _state = TutorialGameplayState.RunningToTrigger;
        private int _currentStepIndex;
        private int _currentActionIndex;
        private Obstacle _trackedObstacle;
        private bool _isGamePausedByTutorial;
        private bool _isSubscribedToUltraUsed;
        private bool _superHitUsed;
        private bool _superHitEffectObserved;
        private bool _automationActionScheduled;
        private float _automationActionTime;
        private Action _primaryCompletionAction;
        private Action _secondaryCompletionAction;
        private Action _automationCompletionAction;
        private bool _automationCompletionActionScheduled;
        private float _automationCompletionActionTime;
        private bool _hasCompletionPresentation;
        private string _completionTitle;
        private string _completionMessage;
        private string _primaryButtonText;
        private string _secondaryButtonText;
        private bool _showPrimaryButton;

        private TutorialGameplayStep CurrentStep => _steps[_currentStepIndex];
        private TutorialAction CurrentExpectedAction => CurrentStep.ExpectedActions[_currentActionIndex];

        public TutorialGameplayController(
            GameManager gameManager,
            Hamster hamster,
            TutorialGameplayScenario scenario)
        {
            _gameManager = gameManager != null
                ? gameManager
                : throw new ArgumentNullException(nameof(gameManager));
            _hamster = hamster != null
                ? hamster
                : throw new ArgumentNullException(nameof(hamster));
            _scenario = scenario;
            _steps = TutorialGameplayStepCatalog.GetSteps(scenario);
            HelpMethods.ApplyOverrideController(_hamster);

            if (_scenario == TutorialGameplayScenario.SuperHit)
            {
                PrepareSuperHitScenario();
            }
        }

        public event Action ScenarioCompleted;
        public event Action SkipRequested;

        public TutorialGameplayScenario Scenario => _scenario;

        /// <summary>
        /// Подключает tutorial UI к корню игрового экрана.
        /// </summary>
        public void Attach(VisualElement root)
        {
            if (root == null || _state == TutorialGameplayState.Disposed)
            {
                return;
            }

            if (ReferenceEquals(root, _attachedRoot) && _view != null)
            {
                return;
            }

            DetachView();
            _attachedRoot = root;
            _view = new TutorialGameplayView(root);
            _view.GameplayActionRequested += HandleViewGameplayAction;
            _view.SkipRequested += HandleViewSkipRequested;
            _view.PrimaryCompletionRequested += HandlePrimaryCompletionRequested;
            _view.SecondaryCompletionRequested += HandleSecondaryCompletionRequested;
            RestoreViewState();
        }

        /// <summary>
        /// Обновляет текущий gameplay-сценарий.
        /// </summary>
        public void Tick()
        {
            switch (_state)
            {
                case TutorialGameplayState.RunningToTrigger:
                    TryPauseAtTutorialObstacle();
                    break;
                case TutorialGameplayState.WaitingForInput:
                    TryPerformAutomationAction();
                    break;
                case TutorialGameplayState.ResolvingAction:
                    TryResolveCurrentStep();
                    break;
                case TutorialGameplayState.Completed:
                    TryPerformAutomationCompletionAction();
                    break;
            }
        }

        /// <summary>
        /// Проверяет действие игрока. Возвращает true, если gameplay может выполнить действие.
        /// </summary>
        public bool TryHandleInput(TutorialAction action)
        {
            if (_state != TutorialGameplayState.WaitingForInput || action != CurrentExpectedAction)
            {
                return false;
            }

            ResumeGameIfPausedByTutorial();

            if (_currentActionIndex + 1 < CurrentStep.ExpectedActions.Count)
            {
                _currentActionIndex++;
                _view?.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
                return true;
            }

            _view?.HidePrompt();
            _state = TutorialGameplayState.ResolvingAction;
            return true;
        }

        /// <summary>
        /// Показывает стандартное окно завершения с кнопками «Играть» и «Меню».
        /// </summary>
        public void ShowCompletion(
            string title,
            string message,
            Action primaryAction,
            Action secondaryAction)
        {
            ShowCompletionCore(
                title,
                message,
                "Играть",
                "Меню",
                showPrimaryButton: true,
                primaryAction,
                secondaryAction,
                automationAction: null);
        }

        /// <summary>
        /// Показывает окно завершения, заданное внешним orchestrator.
        /// </summary>
        public void ShowCompletion(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText,
            bool showPrimaryButton,
            Action primaryAction,
            Action secondaryAction)
        {
            ShowCompletionCore(
                title,
                message,
                primaryButtonText,
                secondaryButtonText,
                showPrimaryButton,
                primaryAction,
                secondaryAction,
                showPrimaryButton ? primaryAction : secondaryAction);
        }

        private void ShowCompletionCore(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText,
            bool showPrimaryButton,
            Action primaryAction,
            Action secondaryAction,
            Action automationAction)
        {
            if (_state == TutorialGameplayState.Disposed)
            {
                return;
            }

            _completionTitle = title ?? string.Empty;
            _completionMessage = message ?? string.Empty;
            _primaryButtonText = primaryButtonText ?? string.Empty;
            _secondaryButtonText = secondaryButtonText ?? string.Empty;
            _showPrimaryButton = showPrimaryButton;
            _primaryCompletionAction = primaryAction;
            _secondaryCompletionAction = secondaryAction;
            _hasCompletionPresentation = true;

            _view?.ShowCompletion(
                _completionTitle,
                _completionMessage,
                _primaryButtonText,
                _secondaryButtonText,
                _showPrimaryButton);

            ScheduleAutomationCompletionAction(automationAction);
        }

        public void Dispose()
        {
            if (_state == TutorialGameplayState.Disposed)
            {
                return;
            }

            ResumeGameIfPausedByTutorial();
            UnsubscribeFromUltraUsed();
            DetachView();
            ClearCompletionActions();
            _superHitTargets.Clear();
            _state = TutorialGameplayState.Disposed;
            ScenarioCompleted = null;
            SkipRequested = null;
        }

        private void PrepareSuperHitScenario()
        {
            _hamster.AddUltaCharge(100);
            GameEventsManager.OnUltaUsed += HandleUltraUsed;
            _isSubscribedToUltraUsed = true;
        }

        private void RestoreViewState()
        {
            if (_hasCompletionPresentation)
            {
                _view.ShowCompletion(
                    _completionTitle,
                    _completionMessage,
                    _primaryButtonText,
                    _secondaryButtonText,
                    _showPrimaryButton);
                return;
            }

            if (_state == TutorialGameplayState.Completed)
            {
                _view.Hide();
                return;
            }

            _view.ShowHeader(CurrentStep.Title);
            if (_state == TutorialGameplayState.WaitingForInput)
            {
                _view.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
            }
        }

        private void TryPauseAtTutorialObstacle()
        {
            if (_gameManager.State != GameState.PLAYING || ObstacleSpawner.Instance == null)
            {
                return;
            }

            Obstacle obstacle = FindNextSameLineObstacle();
            if (obstacle == null)
            {
                return;
            }

            float distance = GetDistanceToHamster(obstacle);
            if (distance < 0f || distance > CurrentStep.PauseDistance)
            {
                return;
            }

            _trackedObstacle = obstacle;
            if (_scenario == TutorialGameplayScenario.SuperHit)
            {
                CaptureSuperHitTargets();
            }

            _currentActionIndex = 0;
            _state = TutorialGameplayState.WaitingForInput;
            PauseGameForTutorial();
            _view?.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
            ScheduleAutomationAction(_automationActionDelaySeconds);
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

        private void TryResolveCurrentStep()
        {
            if (_scenario == TutorialGameplayScenario.SuperHit)
            {
                if (IsSuperHitResolutionComplete())
                {
                    CompleteCurrentStep();
                }

                return;
            }

            if (HasReachedRequiredHamsterState() || HasTrackedObstacleLeftPlay())
            {
                CompleteCurrentStep();
            }
        }

        private bool HasReachedRequiredHamsterState()
        {
            return CurrentStep.CompletionState.HasValue
                   && _hamster.HamsterState.Value == CurrentStep.CompletionState.Value;
        }

        private bool HasTrackedObstacleLeftPlay()
        {
            return _trackedObstacle == null
                   || IsTrackedObstacleOutOfPlay()
                   || IsTrackedObstaclePastLeftScreenEdge();
        }

        private bool IsTrackedObstacleOutOfPlay()
        {
            return ObstacleSpawner.Instance == null
                   || ObstacleSpawner.Instance.SpawnedObstacles.All(
                       spawned => spawned?.ObstacleScript != _trackedObstacle);
        }

        private bool IsTrackedObstaclePastLeftScreenEdge()
        {
            Camera mainCamera = Camera.main;
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

            float screenLeftEdge = mainCamera.transform.position.x
                                   - mainCamera.orthographicSize * mainCamera.aspect;
            return obstacleRightX < screenLeftEdge;
        }

        private void CompleteCurrentStep()
        {
            _trackedObstacle = null;
            _currentActionIndex = 0;
            _automationActionScheduled = false;

            if (ShouldStopAfterCurrentStep() || _currentStepIndex + 1 >= _steps.Count)
            {
                CompleteScenario();
                return;
            }

            _currentStepIndex++;
            _state = TutorialGameplayState.RunningToTrigger;
            _view?.ShowHeader(CurrentStep.Title);
        }

        private bool ShouldStopAfterCurrentStep()
        {
            return _scenario == TutorialGameplayScenario.CoreControls
                   && TutorialAutomation.TryGetStopAfterStep(out int stopAfterStep)
                   && stopAfterStep == CurrentStep.Number;
        }

        private void CompleteScenario()
        {
            if (_state == TutorialGameplayState.Completed)
            {
                return;
            }

            _state = TutorialGameplayState.Completed;
            PauseGameForTutorial();
            UnsubscribeFromUltraUsed();
            _view?.Hide();
            ScenarioCompleted?.Invoke();
        }

        private void CaptureSuperHitTargets()
        {
            _superHitTargets.Clear();
            _superHitTargets.AddRange(FindSuperHitTargetsInRange());
            _superHitUsed = false;
            _superHitEffectObserved = false;
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
                .Where(obstacle => Mathf.Abs(_hamster.transform.position.x - obstacle.transform.position.x)
                                   <= Consts.StrikeRangeMax)
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

            return _superHitEffectObserved && !HasCapturedSuperHitTargetInPlay();
        }

        private static bool IsElectricStrikeEffectPlaying()
        {
            return UnityEngine.Object.FindAnyObjectByType<global::ElectricStrikeUlta>(
                FindObjectsInactive.Include) != null;
        }

        private bool HasCapturedSuperHitTargetInPlay()
        {
            if (_superHitTargets.Count == 0 || ObstacleSpawner.Instance == null)
            {
                return false;
            }

            return ObstacleSpawner.Instance.SpawnedObstacles
                .Select(spawned => spawned?.ObstacleScript)
                .Any(obstacle => obstacle != null && _superHitTargets.Contains(obstacle));
        }

        private void HandleUltraUsed()
        {
            if (_scenario == TutorialGameplayScenario.SuperHit
                && _state == TutorialGameplayState.ResolvingAction)
            {
                _superHitUsed = true;
            }
        }

        private void HandleViewGameplayAction(TutorialAction action)
        {
            if (TryHandleInput(action))
            {
                ForwardGameplayAction(action);
            }
        }

        private void ForwardGameplayAction(TutorialAction action)
        {
            switch (action)
            {
                case TutorialAction.Tap:
                    _hamster.TapRequest?.Invoke();
                    break;
                case TutorialAction.Jump:
                    ForwardJump();
                    break;
                case TutorialAction.SuperJump:
                    ForwardSuperJump();
                    break;
                case TutorialAction.Ultra:
                    _hamster.UltaEvent?.Invoke();
                    break;
            }
        }

        private void ForwardJump()
        {
            if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
            {
                _hamster.RoofJumpRequest?.Invoke();
                return;
            }

            _hamster.JumpRequest?.Invoke();
        }

        private void ForwardSuperJump()
        {
            if (IsJumpingFromRoof())
            {
                _hamster.SuperRoofJumpRequest?.Invoke();
                return;
            }

            _hamster.SuperJumpRequest?.Invoke();
        }

        private bool IsJumpingFromRoof()
        {
            HamsterStateEnum state = _hamster.HamsterState.Value;
            return state == HamsterStateEnum.RoofJump
                   || state == HamsterStateEnum.RoofJumpDamage
                   || state == HamsterStateEnum.JumpFromRoof
                   || state == HamsterStateEnum.JumpFromRoofDamage
                   || state == HamsterStateEnum.JumpOnObstacleFromRoof;
        }

        private void PauseGameForTutorial()
        {
            if (_isGamePausedByTutorial)
            {
                return;
            }

            _gameManager.Pause();
            _isGamePausedByTutorial = true;
        }

        private void ResumeGameIfPausedByTutorial()
        {
            if (!_isGamePausedByTutorial)
            {
                return;
            }

            if (_gameManager != null)
            {
                _gameManager.Resume();
            }

            _isGamePausedByTutorial = false;
        }

        private void ScheduleAutomationAction(float delaySeconds)
        {
            if (!TutorialAutomation.ShouldAutoPlay())
            {
                return;
            }

            _automationActionScheduled = true;
            _automationActionTime = Time.unscaledTime + delaySeconds;
        }

        private void TryPerformAutomationAction()
        {
            if (!_automationActionScheduled || Time.unscaledTime < _automationActionTime)
            {
                return;
            }

            _automationActionScheduled = false;
            TutorialAction expectedAction = CurrentExpectedAction;
            if (!TryHandleInput(expectedAction))
            {
                return;
            }

            ForwardGameplayAction(expectedAction);
            if (_state == TutorialGameplayState.WaitingForInput)
            {
                ScheduleAutomationAction(_automationUpgradeDelaySeconds);
            }
        }

        private void ScheduleAutomationCompletionAction(Action action)
        {
            _automationCompletionAction = null;
            _automationCompletionActionScheduled = false;
            if (!TutorialAutomation.ShouldAutoPlay() || action == null)
            {
                return;
            }

            _automationCompletionAction = action;
            _automationCompletionActionScheduled = true;
            _automationCompletionActionTime = Time.unscaledTime + _automationCompletionDelaySeconds;
        }

        private void TryPerformAutomationCompletionAction()
        {
            if (!_automationCompletionActionScheduled || Time.unscaledTime < _automationCompletionActionTime)
            {
                return;
            }

            ExecuteCompletionAction(_automationCompletionAction);
        }

        private void HandleViewSkipRequested()
        {
            Action skipRequested = SkipRequested;
            if (skipRequested == null)
            {
                return;
            }

            ResumeGameIfPausedByTutorial();
            skipRequested.Invoke();
        }

        private void HandlePrimaryCompletionRequested()
        {
            ExecuteCompletionAction(_primaryCompletionAction);
        }

        private void HandleSecondaryCompletionRequested()
        {
            ExecuteCompletionAction(_secondaryCompletionAction);
        }

        private void ExecuteCompletionAction(Action action)
        {
            if (action == null)
            {
                return;
            }

            _automationCompletionAction = null;
            _automationCompletionActionScheduled = false;
            ResumeGameIfPausedByTutorial();
            action.Invoke();
        }

        private void ClearCompletionActions()
        {
            _primaryCompletionAction = null;
            _secondaryCompletionAction = null;
            _automationCompletionAction = null;
            _automationCompletionActionScheduled = false;
        }

        private void UnsubscribeFromUltraUsed()
        {
            if (!_isSubscribedToUltraUsed)
            {
                return;
            }

            GameEventsManager.OnUltaUsed -= HandleUltraUsed;
            _isSubscribedToUltraUsed = false;
        }

        private void DetachView()
        {
            if (_view == null)
            {
                return;
            }

            _view.GameplayActionRequested -= HandleViewGameplayAction;
            _view.SkipRequested -= HandleViewSkipRequested;
            _view.PrimaryCompletionRequested -= HandlePrimaryCompletionRequested;
            _view.SecondaryCompletionRequested -= HandleSecondaryCompletionRequested;
            _view.Dispose();
            _view = null;
            _attachedRoot = null;
        }
    }
}
