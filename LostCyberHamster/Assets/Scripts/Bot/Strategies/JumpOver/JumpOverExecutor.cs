using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Diagnostics;
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
                return Cancel(action, "reason=invalid-jump-over-action");

            if (hamster.Energy.Value < action.EnergyCost)
                return Cancel(action, $"reason=not-enough-energy energy={hamster.Energy.Value} cost={action.EnergyCost}");

            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : Cancel(action, $"reason=invalid-state state={hamster.HamsterState.Value}");
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

            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.JumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        private static ActionFireResult Cancel(PlannedAction action, string reason)
        {
            HamsterActionLogger.LogCancel(action, reason);
            return ActionFireResult.Cancelled;
        }

        /// <summary>
        /// Проверяет, что runtime jump-over завершился возвратом в run state.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);

            return completed;
        }
    }
}
