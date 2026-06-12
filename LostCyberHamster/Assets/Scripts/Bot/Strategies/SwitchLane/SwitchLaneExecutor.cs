using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Gameplay;

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
                return Cancel(action, "reason=invalid-switch-lane-action");
            }

            // Проверяет, можно ли сейчас принять tap.
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

            // Сверяет ожидаемую линию после tap.
            if (hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value)
            {
                return Cancel(
                    action,
                    $"reason=already-on-target-lane lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
                    $"target={(action.TargetBottomLine.Value ? "bottom" : "top")}");
            }

            // Проверяет окно срабатывания по триггеру.
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

            // Логирует и отправляет tap.
            FireSwitchLane(hamster, action, obstacleLeftX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Пишет причину отмены SwitchLane и возвращает Cancelled.
        /// </summary>
        private static ActionFireResult Cancel(PlannedAction action, string reason)
        {
            HamsterActionLogger.LogCancel(action, reason);
            return ActionFireResult.Cancelled;
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
