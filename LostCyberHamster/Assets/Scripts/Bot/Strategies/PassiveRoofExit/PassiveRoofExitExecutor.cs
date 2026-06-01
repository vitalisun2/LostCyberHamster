using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Исполняет passive roof exit без пользовательского ввода.
    /// </summary>
    internal sealed class PassiveRoofExitExecutor : IActionExecutionHandler
    {
        private readonly PassiveRoofExitPolicy _policy;

        public PassiveRoofExitExecutor(PassiveRoofExitPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Фиксирует начало ожидания естественного схода с крыши.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || action.Kind != _policy.ActionKind)
                return ActionFireResult.Cancelled;

            HamsterStateEnum state = hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
            {
                HamsterActionLogger.LogCancel(action, $"state={state} isDamaged={hamster.IsDamaged.Value}");
                return ActionFireResult.Cancelled;
            }

            if (state != HamsterStateEnum.RoofRun
                && state != HamsterStateEnum.RunFromRoof
                && state != HamsterStateEnum.Run)
            {
                HamsterActionLogger.LogCancel(action, $"unexpectedState={state}");
                return ActionFireResult.Cancelled;
            }

            HamsterActionLogger.LogFire(action, action.RenderWorldX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Завершает действие, когда runtime перешел в обычный Run.
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
