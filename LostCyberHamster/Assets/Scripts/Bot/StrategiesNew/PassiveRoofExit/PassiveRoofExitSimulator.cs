using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.PassiveRoofExit
{
    /// <summary>
    /// Симулирует role-based переход RoofRun -> Run после пассивного схода с крыши.
    /// </summary>
    internal sealed class PassiveRoofExitSimulator : ISimulator
    {
        /// <summary>
        /// Policy passive roof exit action.
        /// </summary>
        private readonly PassiveRoofExitPolicy _policy;

        public PassiveRoofExitSimulator(PassiveRoofExitPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает тип действия passive roof exit.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Симулирует завершенный passive roof exit с переходом в ground Run.
        /// </summary>
        public PlanningState Simulate(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет action contract.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Применяет ground Run состояние после пассивного схода.
            HamsterSnapshot nextHamster = ApplyRunAfterPassiveExit(planningState.Hamster);
            return PlanningStateTransition.Advance(planningState, action, worldSnapshot, nextHamster);
        }

        /// <summary>
        /// Проецирует уже начатое ожидание passive roof exit.
        /// </summary>
        public PlanningState ProjectInProgress(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            // Проверяет action contract.
            if (planningState == null || action == null || worldSnapshot == null || action.Kind != ActionKind)
                return null;

            // Проецирует ожидаемое ground Run состояние.
            HamsterSnapshot nextHamster = ApplyRunAfterPassiveExit(planningState.Hamster);
            return InProgressProjectionHelper.Project(
                planningState,
                action,
                worldSnapshot,
                nextHamster,
                skipTargetObstacleAfterCompletion: false);
        }

        /// <summary>
        /// Создает snapshot хомяка после завершения passive roof exit.
        /// </summary>
        private static HamsterSnapshot ApplyRunAfterPassiveExit(HamsterSnapshot hamster)
        {
            // Возвращает ground-state snapshot на той же линии.
            return new HamsterSnapshot(
                HamsterStateEnum.Run,
                hamster.IsOnBottomLine,
                isOnRoof: false,
                hamster.Energy,
                hamster.Lives,
                isShifting: false,
                roofSupportInstanceId: null,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);
        }
    }
}
