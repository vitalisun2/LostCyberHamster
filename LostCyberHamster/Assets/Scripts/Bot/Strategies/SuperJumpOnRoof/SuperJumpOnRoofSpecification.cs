using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Проверяет применимость super jump on roof.
    /// </summary>
    internal sealed class SuperJumpOnRoofSpecification
    {
        public const int EnergyCost = 20;

        public bool IsSatisfiedBy(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex)
        {
            return TryGetRoofChain(
                planningState,
                decisionPoint,
                out targetObstacle,
                out targetObstacleIndex,
                out _);
        }

        /// <summary>
        /// Ищет первую доступную roof target внутри текущей obstacle chain для super jump.
        /// </summary>
        public bool TryGetRoofChain(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetChainIndex)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetChainIndex = -1;

            if (planningState == null
                || decisionPoint == null
                || decisionPoint.Chain == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof || hamster.IsShifting || hamster.IsDamaged || hamster.Energy < EnergyCost)
                return false;

            if (!decisionPoint.Chain.TryFindFirstRoof(out targetObstacle, out targetObstacleIndex, out targetChainIndex))
                return false;

            if (decisionPoint.Chain.HasDamagingRoofOccupant(targetChainIndex))
                return false;

            for (int chainIndex = 0; chainIndex < targetChainIndex; chainIndex++)
            {
                if (!decisionPoint.Chain.TryGetAt(chainIndex, out ObstacleSnapshot obstacle, out _))
                    return false;

                if (obstacle.IsBottomLine != targetObstacle.IsBottomLine)
                    return false;

                if (!ObstacleClassifier.CanSuperJumpOverOnGround(obstacle.ObstacleType))
                    return false;
            }

            return true;
        }
    }
}