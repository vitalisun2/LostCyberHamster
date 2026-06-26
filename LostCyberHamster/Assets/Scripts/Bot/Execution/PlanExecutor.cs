using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Execution
{
    /// <summary>
    /// Описывает результат одного execution tick текущего плана.
    /// </summary>
    [Flags]
    public enum PlanExecutionTickResult
    {
        None = 0,
        Fired = 1,
        Completed = 2,
        Cancelled = 4
    }

    /// <summary>
    /// Исполняет role-based план бота по одному действию за раз.
    /// </summary>
    public sealed class PlanExecutor
    {
        private readonly IReadOnlyDictionary<BotActionKind, IActionExecutionHandler> _handlers;

        private bool _isActionInProgress;
        private bool _isHeadWaitingForFire;

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
                        $"Для role-based strategy зарегистрировано больше одного executor: kind={strategy.ActionKind}");
                }

                handlers.Add(strategy.ActionKind, strategy.Executor);
            }

            _handlers = handlers;
        }

        /// <summary>
        /// Текущий role-based план на исполнении.
        /// </summary>
        public BotPlan CurrentPlan { get; private set; } = BotPlan.Empty();

        /// <summary>
        /// Флаг активного действия.
        /// </summary>
        public bool IsActionInProgress => _isActionInProgress;

        /// <summary>
        /// Признак head-action, который уже передан execution-слою и не должен заменяться replan-ом.
        /// </summary>
        public bool IsHeadCommitted => _isActionInProgress || _isHeadWaitingForFire;

        /// <summary>
        /// Устанавливает новый role-based план на исполнение и сбрасывает состояние текущего действия.
        /// </summary>
        public void SetPlan(BotPlan plan)
        {
            bool canPreserveCurrentHead =
                CurrentPlan.HasActions
                && plan != null
                && plan.HasActions
                && CurrentPlan.Actions[0].IsEquivalentTo(plan.Actions[0]);
            bool preserveInProgressHead = _isActionInProgress && canPreserveCurrentHead;
            bool preserveWaitingHead =
                !_isActionInProgress
                && _isHeadWaitingForFire
                && canPreserveCurrentHead;

            CurrentPlan = plan ?? BotPlan.Empty();
            _isActionInProgress = preserveInProgressHead;
            _isHeadWaitingForFire = preserveWaitingHead;
        }

        /// <summary>
        /// Очищает текущий role-based план и возвращает executor в исходное состояние.
        /// </summary>
        public void Clear()
        {
            CurrentPlan = BotPlan.Empty();
            _isActionInProgress = false;
            _isHeadWaitingForFire = false;
        }

        /// <summary>
        /// Исполняет головное действие текущего role-based плана.
        /// </summary>
        public PlanExecutionTickResult Tick(Hamster hamster)
        {
            Guard.ThrowIfNull((hamster, nameof(hamster)));

            if (!CurrentPlan.HasActions)
                return PlanExecutionTickResult.None;

            // Сначала пробует один раз запустить действие из головы плана.
            if (!_isActionInProgress)
                return TryFireCurrentHead(hamster);

            // После запуска ждёт, пока handler подтвердит завершение действия.
            PlannedAction action = CurrentPlan.Actions[0];
            IActionExecutionHandler handler = GetRequiredHandler(action);
            if (handler.IsCompleted(hamster, action))
            {
                AdvanceHead();
                return PlanExecutionTickResult.Completed;
            }

            return PlanExecutionTickResult.None;
        }

        /// <summary>
        /// Пытается запустить текущий head-action без ожидания следующего кадра.
        /// </summary>
        private PlanExecutionTickResult TryFireCurrentHead(Hamster hamster)
        {
            if (!CurrentPlan.HasActions)
                return PlanExecutionTickResult.None;

            PlannedAction action = CurrentPlan.Actions[0];
            IActionExecutionHandler handler = GetRequiredHandler(action);
            ActionFireResult fireResult = handler.TryFire(hamster, action);
            if (fireResult == ActionFireResult.Fired)
            {
                _isActionInProgress = true;
                _isHeadWaitingForFire = false;
                return PlanExecutionTickResult.Fired;
            }

            if (fireResult == ActionFireResult.Cancelled)
            {
                AdvanceHead();
                return PlanExecutionTickResult.Cancelled;
            }

            _isHeadWaitingForFire = true;
            return PlanExecutionTickResult.None;
        }

        /// <summary>
        /// Возвращает handler для действия из головы role-based плана.
        /// </summary>
        private IActionExecutionHandler GetRequiredHandler(PlannedAction action)
        {
            if (action == null)
                throw new InvalidOperationException("Role-based план содержит пустое действие в голове очереди.");

            if (_handlers.TryGetValue(action.Kind, out IActionExecutionHandler handler))
                return handler;

            string message =
                $"Для role-based действия бота не зарегистрирован handler: kind={action.Kind}, desc={action.Description}";
            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Сдвигает role-based план после завершения head-action.
        /// </summary>
        private void AdvanceHead()
        {
            IReadOnlyList<PlannedAction> actions = CurrentPlan.Actions;

            // Если действие было последним, план можно полностью очистить.
            if (actions.Count <= 1)
            {
                CurrentPlan = BotPlan.Empty(CurrentPlan.CommittedBoundaryX);
                _isActionInProgress = false;
                _isHeadWaitingForFire = false;
                return;
            }

            // Иначе перестраивает оставшийся хвост после завершения действия в голове.
            var remainingActions = new List<PlannedAction>(actions.Count - 1);
            for (int actionIndex = 1; actionIndex < actions.Count; actionIndex++)
                remainingActions.Add(actions[actionIndex]);

            CurrentPlan = new BotPlan(remainingActions, CurrentPlan.CommittedBoundaryX, CurrentPlan.Score);
            _isActionInProgress = false;
            _isHeadWaitingForFire = false;
        }
    }
}
