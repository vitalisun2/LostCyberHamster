using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot.StrategiesNew.SwitchLane
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

        /// <summary>
        /// Пытается выполнить действие смены линии в допустимый момент.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет обязательные аргументы.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            // Отбрасывает неподходящие действия.
            if (action.Kind != BotActionKind.SwitchLane
                || !action.TargetBottomLine.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            // Проверяет, можно ли сейчас принять tap.
            if (!TapOutcomeResolver.CanAcceptTap(
                    hamster.HamsterState.Value,
                    hamster.IsShifting.Value))
            {
                return hamster.IsShifting.Value
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            // Сверяет ожидаемую линию после tap.
            if (hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value)
                return ActionFireResult.Cancelled;

            // Проверяет окно срабатывания по триггеру.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            // Логирует и отправляет tap.
            FireSwitchLane(hamster, action, obstacleLeftX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Логирует и отправляет runtime tap для SwitchLane.
        /// </summary>
        private static void FireSwitchLane(Hamster hamster, PlannedAction action, float obstacleLeftX)
        {
            string targetLane = action.TargetBottomLine.Value ? "bottom" : "top";
            HamsterActionLogger.LogFire(action, obstacleLeftX, $"targetLane={targetLane} ");
            hamster.TapRequest.Invoke();
        }

        /// <summary>
        /// Определяет, завершилась ли смена линии для запланированного действия.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Ждёт завершения текущего смещения.
            if (hamster.IsShifting.Value)
                return false;

            // Завершает действие без целевой линии.
            if (!action.TargetBottomLine.HasValue)
                return true;

            // Сверяет фактическую линию и пишет лог завершения.
            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed)
                HamsterActionLogger.LogComplete(action, hamster.IsOnBottomLine.Value);

            return completed;
        }
    }
}
