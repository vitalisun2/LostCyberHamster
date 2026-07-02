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
    /// Выполняет tap смены линии с крыши на крышу или дорогу другой линии.
    /// </summary>
    internal sealed class RoofSwitchLaneExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Проверяет, что action достиг рассчитанного trigger window.
        /// </summary>
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
            // Проверяет обязательный контекст.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            // Проверяет контракт action.
            if (action.Kind != BotActionKind.RoofSwitchLane
                || !action.TargetBottomLine.HasValue)
            {
                return Cancel(action, "reason=invalid-roof-switch-lane-action");
            }

            // Проверяет актуальное roof-run состояние.
            if (hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
            {
                return Cancel(
                    action,
                    $"reason=not-roof-run state={hamster.HamsterState.Value}");
            }

            // Проверяет готовность принять tap.
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

            // Проверяет, что смена линии еще нужна.
            if (hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value)
            {
                return Cancel(
                    action,
                    $"reason=already-on-target-lane lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")}");
            }

            // Проверяет trigger window.
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

            // Запускает runtime tap.
            FireRoofSwitchLane(hamster, action, obstacleLeftX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет завершение фактического смещения на целевую линию.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Ждет завершения shift-анимации.
            if (hamster.IsShifting.Value)
                return false;

            // Завершает некорректно неполный action без проверки линии.
            if (!action.TargetBottomLine.HasValue)
                return true;

            // Проверяет достижение целевой линии.
            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed && !action.ResultRoofSupportInstanceId.HasValue)
            {
                HamsterStateEnum state = hamster.HamsterState.Value;
                if (state == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
                {
                    HamsterActionLogger.LogCancel(action, $"state={state} isDamaged={hamster.IsDamaged.Value}");
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
            // Формирует diagnostic context.
            string targetLane = action.TargetBottomLine.Value ? "bottom" : "top";
            string landing = action.ResultRoofSupportInstanceId.HasValue
                ? $"targetLanding=roof targetRoof={action.ResultRoofSupportInstanceId.Value}"
                : "targetLanding=road";
            HamsterActionLogger.LogFire(
                action,
                obstacleLeftX,
                $"targetLane={targetLane} {landing} ");

            // Отправляет input в gameplay.
            hamster.TapRequest.Invoke();
        }
    }
}
