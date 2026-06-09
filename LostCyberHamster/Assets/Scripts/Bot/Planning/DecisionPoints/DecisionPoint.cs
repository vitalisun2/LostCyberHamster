namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Описывает role-based planning-ситуацию через obstacle chain.
    /// </summary>
    public sealed class DecisionPoint
    {
        /// <summary>
        /// Создает role-based decision point из текущей chain.
        /// </summary>
        public DecisionPoint(ObstacleChain chain)
        {
            Chain = chain;
        }

        public ObstacleChain Chain { get; }
    }
}
