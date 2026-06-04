using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.SwitchLane
{
    /// <summary>
    /// Проверяет применимость смены линии для role-based planning-ситуации.
    /// </summary>
    internal sealed class SwitchLaneSpecificationNew
    {
        /// <summary>
        /// Ищет blocking threat, относительно которого можно построить действие смены линии.
        /// </summary>
        public bool TryFindBlockingThreat(
            PlanningState planningState,
            DecisionPointNew decisionPoint,
            out ObstacleSnapshot threatObstacle,
            out int threatObstacleIndex)
        {
            // Сбрасывает выходные значения и проверяет контекст.
            threatObstacle = null;
            threatObstacleIndex = -1;
            if (planningState?.Hamster == null
                || decisionPoint?.Chain == null)
            {
                return false;
            }

            // Отбрасывает состояния, в которых смену линии планировать нельзя.
            HamsterSnapshot hamster = planningState.Hamster;
            if (!CanPlanSwitchLaneFromRoad(hamster)
                || hamster.IsShifting)
            {
                return false;
            }

            // Выбирает первый blocking threat в focus chain.
            if (!decisionPoint.Chain.TryFindFirstWithRole(
                    ObstacleRole.BlockingThreat,
                    out ObstacleChainElementNew threatElement,
                    out _))
            {
                return false;
            }

            // Возвращает obstacle-угрозу для planning action.
            threatObstacle = threatElement.Obstacle;
            threatObstacleIndex = threatElement.WorldIndex;
            return true;
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
