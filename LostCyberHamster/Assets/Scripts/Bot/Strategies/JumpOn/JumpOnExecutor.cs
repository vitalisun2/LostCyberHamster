using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOn
{
    /// <summary>
    /// Выполняет обычный ground jump-on в runtime.
    /// </summary>
    internal sealed class JumpOnExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Проверяет, наступил ли момент запуска action относительно live obstacle.
        /// </summary>
        private readonly ActionTriggerGate _triggerGate;

        public JumpOnExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить обычный jump-on input в runtime.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            // Проверяет совместимость action.
            if (action.Kind != BotActionKind.JumpOn || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            // Проверяет запас энергии.
            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            // Дожидается состояния, в котором можно прыгнуть.
            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            // Проверяет live trigger.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            // Отправляет runtime input.
            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.JumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет, завершился ли обычный jump-on возвратом в Run.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Проверяет состояние завершения.
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);

            return completed;
        }
    }
}
