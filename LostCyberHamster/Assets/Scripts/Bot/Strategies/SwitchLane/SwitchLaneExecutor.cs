using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Выполняет SwitchLane через одиночный tap.
    /// </summary>
    internal sealed class SwitchLaneExecutor : IActionExecutionHandler
    {
        private readonly ActionTriggerGate _triggerGate;

        public SwitchLaneExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.NotNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.SwitchLane
                || !action.TargetObstacleInstanceId.HasValue
                || !action.TargetBottomLine.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            if (!TapOutcomeResolver.CanAcceptTap(
                    hamster.HamsterState.Value,
                    hamster.IsShifting.Value))
            {
                return hamster.IsShifting.Value || hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            bool targetBottomLineAfterTap = !hamster.IsOnBottomLine.Value;
            if (targetBottomLineAfterTap != action.TargetBottomLine.Value)
                return ActionFireResult.Cancelled;

            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            string targetLane = action.TargetBottomLine.Value ? "bottom" : "top";
            HamsterActionLogger.LogFire(action, obstacleLeftX, $"targetLane={targetLane} ");
            hamster.TapRequest.Invoke();
            return ActionFireResult.Fired;
        }

        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster.IsShifting.Value)
                return false;

            if (!action.TargetBottomLine.HasValue)
                return true;

            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.IsOnBottomLine.Value);

            return completed;
        }
    }
}
