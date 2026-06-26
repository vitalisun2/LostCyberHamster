using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Выполняет jump-on-roof в runtime и ждёт RoofRun.
    /// </summary>
    internal sealed class JumpOnRoofExecutor : IActionExecutionHandler
    {
        private readonly ActionTriggerGate _triggerGate;

        /// <summary>
        /// Создает executor с gate проверки live trigger obstacle.
        /// </summary>
        public JumpOnRoofExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить обычный jump-on-roof input.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.JumpOnRoof
                || !action.TargetObstacleInstanceId.HasValue
                || hamster.Energy.Value < action.EnergyCost)
            {
                return ActionFireResult.Cancelled;
            }

            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX, out string diagnosticReason);
            if (triggerResult != ActionFireResult.Fired)
            {
                LogNonFired(action, hamster, triggerResult, obstacleLeftX, diagnosticReason);
                return triggerResult;
            }

            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.JumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет, завершилась ли посадка переходом в RoofRun.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);

            return completed;
        }

        private static void LogNonFired(
            PlannedAction action,
            Hamster hamster,
            ActionFireResult result,
            float obstacleLeftX,
            string diagnosticReason)
        {
            if (!ShouldLogNonFired(action, result))
                return;

            BotExecutionDiagnostics.LogTriggerGateResult(
                "JUMP_ON_ROOF_TRIGGER_DIAG",
                action,
                hamster,
                result,
                obstacleLeftX,
                diagnosticReason,
                BotDiagnosticLevel.Verbose);
        }

        private static bool ShouldLogNonFired(PlannedAction action, ActionFireResult result)
        {
            return result == ActionFireResult.Cancelled
                || action.Description?.Contains("bigNotAlive") == true;
        }

    }
}
