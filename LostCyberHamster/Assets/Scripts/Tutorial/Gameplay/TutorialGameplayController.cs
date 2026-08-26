using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
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

        private readonly ITutorialGameplayWorldAdapter _world;
        private readonly IReadOnlyList<TutorialGameplayStep> _steps;
        private readonly TutorialTransitionGuard _transitionGuard = new();
        private readonly DoubleJumpDetector _doubleJumpDetector = new();

        private TutorialGameplayView _view;
        private VisualElement _attachedRoot;
        private TutorialGameplayState _state = TutorialGameplayState.RunningToTrigger;
        private int _currentStepIndex;
        private int _currentActionIndex;
        private Obstacle _trackedObstacle;
        private bool _isGamePausedByTutorial;
        private bool _isDoubleJumpUpgradeScheduled;
        private float _doubleJumpUpgradeReadyTime;
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

        public TutorialGameplayController(ITutorialGameplayWorldAdapter world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _steps = TutorialGameplayStepCatalog.Steps;
        }

        public event Action ScenarioCompleted;
        public event Action SkipRequested;

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
                    if (TryProcessDoubleJumpUpgrade())
                    {
                        break;
                    }

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

            if (IsDoubleJumpPairStep())
            {
                return TryHandleDoubleJumpInput(action);
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
            DetachView();
            ClearCompletionActions();
            _state = TutorialGameplayState.Disposed;
            ScenarioCompleted = null;
            SkipRequested = null;
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
            ResetActionInputProgress();
            _state = TutorialGameplayState.WaitingForInput;
            PauseGameForTutorial();
            _view?.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
        }

        private bool TryHandleDoubleJumpInput(TutorialAction action)
        {
            // Первый tap открывает окно пары без gameplay request.
            if (action == TutorialAction.Jump)
            {
                ResetActionInputProgress();
                _doubleJumpDetector.RegisterJump(Time.unscaledTime);
                _currentActionIndex = 1;
                _view?.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
                return false;
            }

            // Поздний второй tap сжигает пару и возвращает шаг к первому tap.
            if (!_doubleJumpDetector.RegisterJump(Time.unscaledTime))
            {
                ResetActionInputProgress();
                _view?.ShowPrompt(CurrentStep.Instruction, CurrentExpectedAction);
                return false;
            }

            // Валидная пара выпускает gameplay и передаёт handler-у двухфазный input.
            _doubleJumpDetector.Reset();
            ResumeGameIfPausedByTutorial();
            _view?.HidePrompt();
            _state = TutorialGameplayState.ResolvingAction;
            return true;
        }

        private bool IsDoubleJumpPairStep()
        {
            return CurrentStep.ExpectedActions.Count == 2
                   && CurrentStep.ExpectedActions[0] == TutorialAction.Jump
                   && CurrentStep.ExpectedActions[1] == TutorialAction.SuperJump;
        }

        private void ResetActionInputProgress()
        {
            _currentActionIndex = 0;
            _doubleJumpDetector.Reset();
            ResetDoubleJumpUpgradeSchedule();
        }

        private bool TryProcessDoubleJumpUpgrade()
        {
            // Без schedule обычный resolve идёт сразу.
            if (!_isDoubleJumpUpgradeScheduled)
            {
                return false;
            }

            // До края игрового окна tutorial удерживает завершение шага.
            if (Time.time < _doubleJumpUpgradeReadyTime)
            {
                return true;
            }

            // На краю окна отправляет второй request и даёт gameplay обработать кадр.
            _world.PerformAction(TutorialAction.SuperJump);
            ResetDoubleJumpUpgradeSchedule();
            return true;
        }

        private void ScheduleDoubleJumpUpgrade()
        {
            _isDoubleJumpUpgradeScheduled = true;
            _doubleJumpUpgradeReadyTime = Time.time + DoubleJumpDetector.DoubleJumpThreshold;
        }

        private void ResetDoubleJumpUpgradeSchedule()
        {
            _isDoubleJumpUpgradeScheduled = false;
            _doubleJumpUpgradeReadyTime = 0f;
        }

        private void TryResolveCurrentStep()
        {
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
            ResetActionInputProgress();
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
            _view?.Hide();
            ScenarioCompleted?.Invoke();
        }

        private void HandleViewGameplayAction(TutorialAction action)
        {
            if (TryHandleInput(action))
            {
                if (IsDoubleJumpPairStep() && action == TutorialAction.SuperJump)
                {
                    _world.PerformAction(TutorialAction.Jump);
                    ScheduleDoubleJumpUpgrade();
                    return;
                }

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
