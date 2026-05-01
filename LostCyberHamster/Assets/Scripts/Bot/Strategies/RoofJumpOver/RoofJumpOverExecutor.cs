using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Выполняет roof jump over в runtime и ждёт возврата в RoofRun.
    /// </summary>
    internal sealed class RoofJumpOverExecutor : IActionExecutionHandler
    {
        private readonly ActionTriggerGate _triggerGate;

        public RoofJumpOverExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.RoofJumpOver || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            if (hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
                return ActionFireResult.Cancelled;

            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.RoofJumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);

            return completed;
        }
    }
}
