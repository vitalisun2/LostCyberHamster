using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Проверяет применимость дорожной смены линии к уже выбранной blocking threat.
    /// </summary>
    internal sealed class SwitchLaneSpecification : IBotStrategySpecification
    {
        /// <summary>
        /// Возвращает true, если SwitchLane применим к указанной blocking threat.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            if (planningState?.Hamster == null
                || obstacle == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (!CanPlanSwitchLaneFromRoad(hamster)
                || hamster.IsShifting)
            {
                return false;
            }

            return obstacle.IsBottomLine == hamster.IsOnBottomLine;
        }

        /// <summary>
        /// Возвращает true, если из текущего состояния можно планировать дорожный SwitchLane.
        /// </summary>
        public bool IsSatisfiedBy(PlanningState planningState)
        {
            if (planningState?.Hamster == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            return CanPlanSwitchLaneFromRoad(hamster)
                && !hamster.IsShifting;
        }

        /// <summary>
        /// Определяет, допускает ли текущее состояние дорожный SwitchLane.
        /// </summary>
        private static bool CanPlanSwitchLaneFromRoad(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.Run
                && !hamster.IsOnRoof;
        }
    }
}
