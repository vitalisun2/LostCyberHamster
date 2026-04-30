using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Содержит helper-логику для сравнения target obstacle внутри decision point.
    /// </summary>
    internal static class DecisionPointExtensions
    {
        /// <summary>
        /// Проверяет, входит ли obstacle в текущую chain decision point.
        /// </summary>
        public static bool MatchesTargetObstacle(this DecisionPoint decisionPoint, ObstacleSnapshot obstacle)
        {
            if (decisionPoint == null || obstacle == null)
                return false;

            return decisionPoint.Chain != null && decisionPoint.Chain.ContainsObstacle(obstacle);
        }
    }
}