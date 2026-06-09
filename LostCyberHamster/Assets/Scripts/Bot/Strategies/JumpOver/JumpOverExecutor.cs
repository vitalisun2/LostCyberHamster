using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Выполняет обычный jump-over в runtime.
    /// </summary>
    internal sealed class JumpOverExecutor : IActionExecutionHandler
    {
        private readonly ActionTriggerGate _triggerGate;

        /// <summary>
        /// Создаёт executor с gate, который проверяет момент runtime fire.
        /// </summary>
        public JumpOverExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить jump-over, если hamster и target obstacle готовы к fire.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.JumpOver || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            ActionFireResult triggerResult = _triggerGate.Check(action, out _);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            hamster.JumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет, что runtime jump-over завершился возвратом в run state.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            return hamster.HamsterState.Value == HamsterStateEnum.Run;
        }
    }
}
