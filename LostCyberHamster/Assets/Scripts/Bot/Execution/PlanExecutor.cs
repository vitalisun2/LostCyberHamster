using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Execution
{
    /// <summary>
    /// Исполняет текущий план бота по одному действию за раз.
    /// </summary>
    public sealed class PlanExecutor
    {
        private readonly IReadOnlyDictionary<BotActionKind, IActionExecutionHandler> _handlers;

        private bool _isActionInProgress;

        internal PlanExecutor(IReadOnlyList<IPlanningStrategy> strategies)
        {
            var handlers = new Dictionary<BotActionKind, IActionExecutionHandler>();
            for (int strategyIndex = 0; strategyIndex < strategies?.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy?.Executor == null)
                    continue;

                if (handlers.ContainsKey(strategy.ActionKind))
                {
                    throw new InvalidOperationException(
                        $"Для strategy зарегистрировано больше одного executor: kind={strategy.ActionKind}");
                }

                handlers.Add(strategy.ActionKind, strategy.Executor);
            }

            _handlers = handlers;
        }

        /// <summary>
        /// Текущий план на исполнении.
        /// </summary>
        public BotPlan CurrentPlan { get; private set; } = BotPlan.Empty();

        /// <summary>
        /// Флаг активного действия.
        /// </summary>
        public bool IsActionInProgress => _isActionInProgress;

        /// <summary>
        /// Устанавливает новый план на исполнение и сбрасывает состояние текущего действия.
        /// </summary>
        public void SetPlan(BotPlan plan)
        {
            bool preserveInProgressHead =
                _isActionInProgress
                && CurrentPlan.HasActions
                && plan != null
                && plan.HasActions
                && CurrentPlan.Actions[0].IsEquivalentTo(plan.Actions[0]);

            CurrentPlan = plan ?? BotPlan.Empty();
            _isActionInProgress = preserveInProgressHead;
        }

        /// <summary>
        /// Очищает текущий план и возвращает executor в исходное состояние.
        /// </summary>
        public void Clear()
        {
            CurrentPlan = BotPlan.Empty();
            _isActionInProgress = false;
        }

        /// <summary>
        /// Исполняет головное действие текущего плана и продвигает план после завершения этого действия.
        /// </summary>
        public void Tick(Hamster hamster)
        {
            Guard.NotNull((hamster, nameof(hamster)));

            if (!CurrentPlan.HasActions)
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

            DebugManager.DiagLog($"[Bot EXEC] ERROR {message}");
            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Сдвигает план после завершения head-action.
        /// </summary>
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