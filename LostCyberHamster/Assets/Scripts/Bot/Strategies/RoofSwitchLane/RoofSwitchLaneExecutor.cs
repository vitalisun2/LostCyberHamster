using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Выполняет tap смены линии между крышами.
    /// </summary>
    internal sealed class RoofSwitchLaneExecutor : IActionExecutionHandler
    {
        private readonly ActionTriggerGate _triggerGate;

        public RoofSwitchLaneExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается запустить roof switch-lane в рассчитанном trigger window.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.RoofSwitchLane
                || !action.TargetBottomLine.HasValue
                || !action.ResultRoofSupportInstanceId.HasValue)
            {
                return Cancel(action, "reason=invalid-roof-switch-lane-action");
            }

            if (hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
            {
                return Cancel(
                    action,
                    $"reason=not-roof-run state={hamster.HamsterState.Value}");
            }

            if (!TapOutcomeResolver.CanAcceptTap(
                    hamster.HamsterState.Value,
                    hamster.IsShifting.Value))
            {
                return hamster.IsShifting.Value
                    ? ActionFireResult.Waiting
                    : Cancel(
                        action,
                        $"reason=tap-not-accepted state={hamster.HamsterState.Value} shifting={hamster.IsShifting.Value}");
            }

            if (hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value)
            {
                return Cancel(
                    action,
                    $"reason=already-on-target-lane lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")}");
            }

            ActionFireResult triggerResult = _triggerGate.Check(
                action,
                out float obstacleLeftX,
                out string triggerDiagnosticReason);
            if (triggerResult != ActionFireResult.Fired)
            {
                if (triggerResult == ActionFireResult.Cancelled)
                {
                    return Cancel(
                        action,
                        $"reason=trigger-gate-cancel obstacleLeftX={obstacleLeftX:F2} {triggerDiagnosticReason}");
                }

                return triggerResult;
            }

            FireRoofSwitchLane(hamster, action, obstacleLeftX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет завершение фактического смещения на целевую линию.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster.IsShifting.Value)
                return false;

            if (!action.TargetBottomLine.HasValue)
                return true;

            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);

            return completed;
        }

        /// <summary>
        /// Пишет причину отмены и возвращает Cancelled.
        /// </summary>
        private static ActionFireResult Cancel(PlannedAction action, string reason)
        {
            HamsterActionLogger.LogCancel(action, reason);
            return ActionFireResult.Cancelled;
        }

        /// <summary>
        /// Логирует и отправляет tap для смены roof lane.
        /// </summary>
        private static void FireRoofSwitchLane(Hamster hamster, PlannedAction action, float obstacleLeftX)
        {
            string targetLane = action.TargetBottomLine.Value ? "bottom" : "top";
            HamsterActionLogger.LogFire(
                action,
                obstacleLeftX,
                $"targetLane={targetLane} targetRoof={action.ResultRoofSupportInstanceId.Value} ");
            hamster.TapRequest.Invoke();
        }
    }
}
