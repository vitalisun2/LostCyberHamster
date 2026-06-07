using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.PassiveRoofExit
{
    /// <summary>
    /// Исполняет role-based passive roof exit без пользовательского ввода.
    /// </summary>
    internal sealed class PassiveRoofExitExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Policy passive roof exit action.
        /// </summary>
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
            // Проверяет action contract.
            if (hamster == null || action == null || action.Kind != _policy.ActionKind)
                return ActionFireResult.Cancelled;

            // Отсекает поврежденное или мертвое состояние.
            HamsterStateEnum state = hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
            {
                HamsterActionLogger.LogCancel(action, $"state={state} isDamaged={hamster.IsDamaged.Value}");
                return ActionFireResult.Cancelled;
            }

            // Разрешает только состояния естественного roof-exit lifecycle.
            if (state != HamsterStateEnum.RoofRun
                && state != HamsterStateEnum.RunFromRoof
                && state != HamsterStateEnum.Run)
            {
                HamsterActionLogger.LogCancel(action, $"unexpectedState={state}");
                return ActionFireResult.Cancelled;
            }

            // Помечает no-input action как начатый.
            HamsterActionLogger.LogFire(action, action.RenderWorldX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Завершает действие, когда runtime перешел в обычный Run.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Проверяет action contract.
            if (hamster == null || action == null || action.Kind != _policy.ActionKind)
                return false;

            // Завершает action при damage/death, чтобы executor не завис.
            HamsterStateEnum state = hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
            {
                HamsterActionLogger.LogCancel(action, $"state={state} isDamaged={hamster.IsDamaged.Value}");
                return true;
            }

            // Ждет фактический ground Run.
            if (state == HamsterStateEnum.Run)
            {
                HamsterActionLogger.LogComplete(action, state);
                return true;
            }

            if (state == HamsterStateEnum.RoofRun || state == HamsterStateEnum.RunFromRoof)
                return false;

            // Сбрасывает action при неожиданном runtime state.
            HamsterActionLogger.LogCancel(action, $"unexpectedState={state}");
            return true;
        }
    }
}
