using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.RoofJumpOver
{
    /// <summary>
    /// Выполняет обычный roof jump-over в runtime и ждёт возврата в RoofRun.
    /// </summary>
    internal sealed class RoofJumpOverExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Gate проверки live trigger obstacle перед отправкой input.
        /// </summary>
        private readonly ActionTriggerGate _triggerGate;

        /// <summary>
        /// Создает executor с gate проверки live trigger obstacle.
        /// </summary>
        public RoofJumpOverExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить roof jump request для roof jump-over.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет вход и action contract.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.RoofJumpOver
                || !action.TargetObstacleInstanceId.HasValue
                || hamster.Energy.Value < action.EnergyCost
                || hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
            {
                return ActionFireResult.Cancelled;
            }

            // Проверяет live trigger и отправляет input.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.RoofJumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет завершение roof jump-over возвратом в RoofRun.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Проверяет возврат runtime state в RoofRun.
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);

            return completed;
        }
    }
}
