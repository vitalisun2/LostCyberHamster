namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Единый контракт построителя chain для конкретной planning-ситуации.
    /// </summary>
    internal interface IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить decision point с готовой obstacle chain.
        /// </summary>
        bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint);
    }
}
