using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Проверяет применимость roof jump-over к выбранному roof occupant hazard.
    /// </summary>
    internal sealed class RoofJumpOverSpecification
    {
        /// <summary>
        /// Возвращает true, если хомяк может выполнить roof jump-over над obstacle.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot hazardObstacle)
        {
            // Проверяет context и выбранный hazard.
            if (planningState?.Hamster == null || hazardObstacle == null)
                return false;

            // Проверяет roof-run состояние.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.HamsterState != HamsterStateEnum.RoofRun
                || !hamster.IsOnRoof
                || !hamster.RoofSupportInstanceId.HasValue
                || hamster.IsShifting)
            {
                return false;
            }

            return hazardObstacle.IsBottomLine == hamster.IsOnBottomLine;
        }
    }
}
