using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.JumpFromRoof
{
    /// <summary>
    /// Выполняет обычный прыжок с крыши на дорогу в runtime.
    /// </summary>
    internal sealed class JumpFromRoofExecutor : IActionExecutionHandler
    {
        private readonly ActionTriggerGate _triggerGate;

        /// <summary>
        /// Создает executor с gate проверки live trigger obstacle.
        /// </summary>
        public JumpFromRoofExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить roof jump request для прыжка с крыши.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет вход и action contract.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (action.Kind != BotActionKind.JumpFromRoof
                || !action.TargetObstacleInstanceId.HasValue
                || hamster.Energy.Value < action.EnergyCost)
            {
                return ActionFireResult.Cancelled;
            }

            // Проверяет roof-run состояние.
            if (hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
                return ActionFireResult.Cancelled;

            // Проверяет live trigger и отправляет input.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.RoofJumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет завершение прыжка с крыши возвратом в Run.
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
