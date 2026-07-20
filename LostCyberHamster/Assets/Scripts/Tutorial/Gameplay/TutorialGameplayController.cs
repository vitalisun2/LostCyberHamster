using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Gameplay;
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

        private readonly ITutorialGameplayWorldAdapter _world;
        private readonly TutorialGameplayScenario _scenario;
        private readonly IReadOnlyList<TutorialGameplayStep> _steps;
        private readonly List<Obstacle> _superHitTargets = new();
        private readonly TutorialTransitionGuard _transitionGuard = new();

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
        private Action _primaryCompletionAction;
        private Action _secondaryCompletionAction;
        private bool _hasCompletionPresentation;
        private string _completionTitle;
        private string _completionMessage;
        private string _primaryButtonText;
        private string _secondaryButtonText;
        private bool _showPrimaryButton;

        private TutorialGameplayStep CurrentStep => _steps[_currentStepIndex];
        private TutorialAction CurrentExpectedAction => CurrentStep.ExpectedActions[_currentActionIndex];

        public TutorialGameplayController(
            ITutorialGameplayWorldAdapter world,
            TutorialGameplayScenario scenario)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _scenario = scenario;
            _steps = TutorialGameplayStepCatalog.GetSteps(scenario);
            _world.Prepare(scenario);

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
                    break;
                case TutorialGameplayState.ResolvingAction:
                    TryResolveCurrentStep();
                    break;
                case TutorialGameplayState.Completed:
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
                secondaryAction);
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
                secondaryAction);
        }

        private void ShowCompletionCore(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText,
            bool showPrimaryButton,
            Action primaryAction,
            Action secondaryAction)
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
            _transitionGuard.Reset();

            _view?.ShowCompletion(
                _completionTitle,
                _completionMessage,
                _primaryButtonText,
                _secondaryButtonText,
                _showPrimaryButton);

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
            _world.UltraUsed += HandleUltraUsed;
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
            if (_world.State != Assets.Scripts.GameManagerLogic.GameState.PLAYING)
            {
                return;
            }

            Obstacle obstacle = _world.FindNearestSameLineObstacle(CurrentStep.TargetTypes);
            if (obstacle == null)
            {
                return;
            }

            float distance = _world.GetDistanceToHamster(obstacle);
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
                   && _world.HamsterState == CurrentStep.CompletionState.Value;
        }

        private bool HasTrackedObstacleLeftPlay()
        {
            return _world.HasObstacleLeftPlay(_trackedObstacle);
        }

        private void CompleteCurrentStep()
        {
            _trackedObstacle = null;
            _currentActionIndex = 0;
            if (_currentStepIndex + 1 >= _steps.Count)
            {
                CompleteScenario();
                return;
            }

            _currentStepIndex++;
            _state = TutorialGameplayState.RunningToTrigger;
            _view?.ShowHeader(CurrentStep.Title);
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
            _world.CaptureSuperHitTargets(_superHitTargets);
            _superHitUsed = false;
            _superHitEffectObserved = false;
        }

        private bool IsSuperHitResolutionComplete()
        {
            if (!_superHitUsed)
            {
                return false;
            }

            if (_world.IsElectricStrikeEffectPlaying())
            {
                _superHitEffectObserved = true;
                return false;
            }

            return _superHitEffectObserved && !_world.HasCapturedSuperHitTargetInPlay(_superHitTargets);
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
                _world.PerformAction(action);
            }
        }

        private void PauseGameForTutorial()
        {
            if (_isGamePausedByTutorial)
            {
                return;
            }

            _world.Pause();
            _isGamePausedByTutorial = true;
        }

        private void ResumeGameIfPausedByTutorial()
        {
            if (!_isGamePausedByTutorial)
            {
                return;
            }

            _world.Resume();
            _isGamePausedByTutorial = false;
        }

        private void HandleViewSkipRequested()
        {
            ExecuteTransition(SkipRequested);
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
            ExecuteTransition(action);
        }

        private void ExecuteTransition(Action action)
        {
            if (action == null || !_transitionGuard.TryBegin())
            {
                return;
            }

            ClearCompletionActions();
            _view?.Hide();
            ResumeGameIfPausedByTutorial();
            action.Invoke();
        }

        private void ClearCompletionActions()
        {
            _primaryCompletionAction = null;
            _secondaryCompletionAction = null;
        }

        private void UnsubscribeFromUltraUsed()
        {
            if (!_isSubscribedToUltraUsed)
            {
                return;
            }

            _world.UltraUsed -= HandleUltraUsed;
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
