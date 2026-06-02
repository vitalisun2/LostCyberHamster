using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Выполняет tap смены линии в RoofRun и ждёт завершения RunFromRoof.
    /// </summary>
    internal sealed class RoofSwitchLaneExitExecutor : IActionExecutionHandler
    {
        private readonly RoofSwitchLaneExitPolicy _policy;
        private readonly ActionTriggerGate _triggerGate;

        public RoofSwitchLaneExitExecutor(
            RoofSwitchLaneExitPolicy policy,
            ActionTriggerGate triggerGate)
        {
            _policy = policy;
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Запускает нажатие смены линии, когда obstacle-триггер входит в окно запуска.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != _policy.ActionKind
                || !action.TargetObstacleInstanceId.HasValue
                || !action.TargetBottomLine.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            HamsterStateEnum state = hamster.HamsterState.Value;
            if (!TapOutcomeResolver.CanAcceptTap(state, hamster.IsShifting.Value))
            {
                return hamster.IsShifting.Value
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            if (state != HamsterStateEnum.RoofRun)
                return ActionFireResult.Cancelled;

            if (hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value)
                return ActionFireResult.Cancelled;

            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            string targetLane = action.TargetBottomLine.Value ? "bottom" : "top";
            HamsterActionLogger.LogFire(action, obstacleLeftX, $"targetLane={targetLane} ");
            hamster.TapRequest.Invoke();

            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Возвращает true, когда runtime завершил переход в Run.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || action.Kind != _policy.ActionKind)
                return false;

            HamsterStateEnum state = hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
            {
                HamsterActionLogger.LogCancel(action, $"state={state} isDamaged={hamster.IsDamaged.Value}");
                return true;
            }

            if (hamster.IsShifting.Value)
                return false;

            if (action.TargetBottomLine.HasValue
                && hamster.IsOnBottomLine.Value != action.TargetBottomLine.Value)
            {
                HamsterActionLogger.LogCancel(action, $"unexpectedLane={hamster.IsOnBottomLine.Value}");
                return true;
            }

            if (state == HamsterStateEnum.Run)
            {
                HamsterActionLogger.LogComplete(action, state);
                return true;
            }

            if (state == HamsterStateEnum.RoofRun || state == HamsterStateEnum.RunFromRoof)
                return false;

            HamsterActionLogger.LogCancel(action, $"unexpectedState={state}");
            return true;
        }
    }
}
