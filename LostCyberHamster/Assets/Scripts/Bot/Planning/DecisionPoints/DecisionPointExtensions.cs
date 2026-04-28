using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Содержит helper-логику для выбора и сравнения target obstacle внутри decision point.
    /// </summary>
    internal static class DecisionPointExtensions
    {
        /// <summary>
        /// Возвращает true, если точка решения описывает blocking-угрозу на текущей линии.
        /// </summary>
        public static bool IsBlockingThreat(this DecisionPoint decisionPoint)
        {
            return decisionPoint != null
                   && (decisionPoint.Kind == DecisionPointKind.BlockingObstacle
                       || decisionPoint.Kind == DecisionPointKind.BlockingObstacleWithRoofLanding);
        }

        /// <summary>
        /// Возвращает roof-landing target для стратегий посадки на крышу.
        /// </summary>
        public static bool TryGetRoofLandingTarget(this DecisionPoint decisionPoint, out ObstacleSnapshot obstacle, out int obstacleIndex)
        {
            obstacle = null;
            obstacleIndex = -1;

            if (decisionPoint == null)
                return false;

            if (decisionPoint.HasRoofLandingObstacle)
            {
                obstacle = decisionPoint.RoofLandingObstacle;
                obstacleIndex = decisionPoint.RoofLandingObstacleIndex;
                return true;
            }

            if (decisionPoint.Kind == DecisionPointKind.RoofLanding && decisionPoint.Obstacle != null)
            {
                obstacle = decisionPoint.Obstacle;
                obstacleIndex = decisionPoint.ObstacleIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, совпадает ли obstacle с primary threat или с roof-landing continuation.
        /// </summary>
        public static bool MatchesTargetObstacle(this DecisionPoint decisionPoint, ObstacleSnapshot obstacle)
        {
            if (decisionPoint == null || obstacle == null)
                return false;

            if (decisionPoint.Obstacle != null && decisionPoint.Obstacle.InstanceId == obstacle.InstanceId)
                return true;

            return decisionPoint.RoofLandingObstacle != null
                   && decisionPoint.RoofLandingObstacle.InstanceId == obstacle.InstanceId;
        }
    }
}