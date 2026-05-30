namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит required decision point для ближайшей обычной угрозы на текущей линии.
    /// </summary>
    internal sealed class BlockingThreatChainBuilder : IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить blocking-threat chain.
        /// </summary>
        public bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (!context.HasValidInput)
                return false;

            // Строит chain от ближайшей threat.
            if (!ThreatChainCollector.TryBuildNearestThreatChain(
                    context.PlanningState,
                    context.WorldSnapshot,
                    context.FirstObstacleIndex,
                    out ObstacleChain chain))
            {
                return false;
            }

            decisionPoint = new DecisionPoint(
                chain,
                DecisionPointKind.BlockingThreat,
                isDecisionRequired: true);
            return true;
        }
    }
}
