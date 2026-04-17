using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Execution.Handlers;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Execution
{
    public sealed class PlanExecutor
    {
        private readonly IReadOnlyDictionary<BotActionKind, IActionExecutionHandler> _handlers =
            new Dictionary<BotActionKind, IActionExecutionHandler>
            {
                { BotActionKind.Tap, new SwitchLaneActionHandler() }
            };

        private bool _isActionInProgress;

        public BotPlan CurrentPlan { get; private set; } = BotPlan.Empty();
        public bool IsActionInProgress => _isActionInProgress;
        public bool HasPendingActions => CurrentPlan.HasActions;

        /// <summary>
        /// Устанавливает новый план на исполнение и сбрасывает состояние текущего действия.
        /// </summary>
        public void SetPlan(BotPlan plan)
        {
            CurrentPlan = plan ?? BotPlan.Empty();
            _isActionInProgress = false;
        }

        /// <summary>
        /// Очищает текущий план и возвращает executor в исходное состояние.
        /// </summary>
        public void Clear(float committedBoundaryX = 0f)
        {
            CurrentPlan = BotPlan.Empty(committedBoundaryX);
            _isActionInProgress = false;
        }

        /// <summary>
        /// Исполняет головное действие текущего плана и продвигает план после завершения этого действия.
        /// </summary>
        public void Tick(Hamster hamster)
        {
            if (hamster == null || !CurrentPlan.HasActions)
                return;

            PlannedAction action = CurrentPlan.Actions[0];
            IActionExecutionHandler handler = GetRequiredHandler(action);

            // Сначала пробуем один раз запустить действие из головы плана.
            if (!_isActionInProgress)
            {
                ActionFireResult fireResult = handler.TryFire(hamster, action);
                if (fireResult == ActionFireResult.Fired)
                    _isActionInProgress = true;
                else if (fireResult == ActionFireResult.Cancelled)
                    AdvanceHead();

                return;
            }

            // После запуска ждём, пока handler подтвердит завершение действия.
            if (handler.IsCompleted(hamster, action))
                AdvanceHead();
        }

        /// <summary>
        /// Возвращает handler для действия из головы плана и выбрасывает ошибку, если execution-слой его не поддерживает.
        /// </summary>
        private IActionExecutionHandler GetRequiredHandler(PlannedAction action)
        {
            if (action == null)
                throw new InvalidOperationException("План содержит пустое действие в голове очереди.");

            if (_handlers.TryGetValue(action.Kind, out IActionExecutionHandler handler))
                return handler;

            string message =
                $"Для действия бота не зарегистрирован handler: kind={action.Kind}, desc={action.Description}";

            DebugManager.DiagLog($"[BotV2 EXEC] ERROR {message}");
            throw new InvalidOperationException(message);
        }

        private void AdvanceHead()
        {
            IReadOnlyList<PlannedAction> actions = CurrentPlan.Actions;

            // Если действие было последним, план можно полностью очистить.
            if (actions.Count <= 1)
            {
                CurrentPlan = BotPlan.Empty(CurrentPlan.CommittedBoundaryX);
                _isActionInProgress = false;
                return;
            }

            // Иначе перестраиваем оставшийся хвост после завершения действия в голове.
            var remainingActions = new List<PlannedAction>(actions.Count - 1);
            for (int actionIndex = 1; actionIndex < actions.Count; actionIndex++)
                remainingActions.Add(actions[actionIndex]);

            CurrentPlan = new BotPlan(remainingActions, CurrentPlan.CommittedBoundaryX, CurrentPlan.Score);
            _isActionInProgress = false;
        }
    }
}
