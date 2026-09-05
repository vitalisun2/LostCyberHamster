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
        private Action _completionAction;
        private bool _hasCompletionPresentation;
        private string _completionTitle;
        private string _completionMessage;
        private string _completionButtonText;
        private bool _completionIsError;

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

            if (ReferenceEquals(root, _attachedRoot) && _view?.IsAttachedTo(root) == true)
            {
                return;
            }

            DetachView();
            _attachedRoot = root;
            _view = new TutorialGameplayView(root);
            _view.GameplayActionRequested += HandleViewGameplayAction;
            _view.SkipRequested += HandleViewSkipRequested;
            _view.CompletionRequested += HandleCompletionRequested;
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
                _view?.ShowPrompt(CurrentStep.InstructionKey, CurrentExpectedAction);
                return true;
            }

            _view?.HidePrompt();
            _state = TutorialGameplayState.ResolvingAction;
            return true;
        }

        /// <summary>Показывает сохранённое завершение или ошибку с одним действием владельца flow.</summary>
        public void ShowCompletion(
            string title,
            string message,
            string buttonText,
            Action completionAction,
            bool isError = false)
        {
            if (_state == TutorialGameplayState.Disposed)
            {
                return;
            }

            // Completion удерживает мир даже после Skip с ошибкой сохранения.
            _state = TutorialGameplayState.Completed;
            ResetActionInputProgress();
            PauseGameForTutorial();

            // Новая presentation заменяет Retry на Play и открывает её собственный переход.
            _completionTitle = title ?? string.Empty;
            _completionMessage = message ?? string.Empty;
            _completionButtonText = buttonText ?? string.Empty;
            _completionAction = completionAction ?? throw new ArgumentNullException(nameof(completionAction));
            _completionIsError = isError;
            _hasCompletionPresentation = true;
            _transitionGuard.Reset();
            _view?.ShowCompletion(
                _completionTitle, _completionMessage, _completionButtonText, _completionIsError);
        }

        /// <summary>Освобождает controller после смены сцены без обращения к прежнему миру.</summary>
        public void DisposeAfterSceneChange()
        {
            _isGamePausedByTutorial = false;
            Dispose();
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
                    _completionTitle, _completionMessage, _completionButtonText, _completionIsError);
                return;
            }

            if (_state == TutorialGameplayState.Completed)
            {
                _view.Hide();
                return;
            }

            _view.ShowHeader(CurrentStep.TitleKey, CurrentStep.Number);
            if (_state == TutorialGameplayState.WaitingForInput)
            {
                _view.ShowPrompt(CurrentStep.InstructionKey, CurrentExpectedAction);
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
            _view?.ShowPrompt(CurrentStep.InstructionKey, CurrentExpectedAction);
        }

        private bool TryHandleDoubleJumpInput(TutorialAction action)
        {
            // Первый tap открывает окно пары без gameplay request.
            if (action == TutorialAction.Jump)
            {
                ResetActionInputProgress();
                _doubleJumpDetector.RegisterJump(Time.unscaledTime);
                _currentActionIndex = 1;
                _view?.ShowPrompt(CurrentStep.InstructionKey, CurrentExpectedAction);
                return false;
            }

            // Поздний второй tap сжигает пару и возвращает шаг к первому tap.
            if (!_doubleJumpDetector.RegisterJump(Time.unscaledTime))
            {
                ResetActionInputProgress();
                _view?.ShowPrompt(CurrentStep.InstructionKey, CurrentExpectedAction);
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
            _view?.ShowHeader(CurrentStep.TitleKey, CurrentStep.Number);
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
            if (_state == TutorialGameplayState.Completed || _state == TutorialGameplayState.Disposed)
            {
                return;
            }

            ExecuteTransition(SkipRequested);
        }

        private void HandleCompletionRequested()
        {
            ExecuteTransition(_completionAction);
        }

        /// <summary>Передаёт переход flow при удерживаемой паузе и защите от повторного клика.</summary>
        private void ExecuteTransition(Action action)
        {
            if (_state == TutorialGameplayState.Disposed || action == null || !_transitionGuard.TryBegin())
            {
                return;
            }

            // Вызов может заменить Retry на Play; его новая presentation остаётся владельцем UI.
            PauseGameForTutorial();
            try
            {
                action.Invoke();
            }
            catch
            {
                // Исключение оставляет текущее действие доступным для повторной попытки.
                _transitionGuard.Reset();
                throw;
            }
        }

        private void ClearCompletionActions()
        {
            _completionAction = null;
            _hasCompletionPresentation = false;
        }

        private void DetachView()
        {
            if (_view == null)
            {
                return;
            }

            _view.GameplayActionRequested -= HandleViewGameplayAction;
            _view.SkipRequested -= HandleViewSkipRequested;
            _view.CompletionRequested -= HandleCompletionRequested;
            _view.Dispose();
            _view = null;
            _attachedRoot = null;
        }
    }
}
