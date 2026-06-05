namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Описывает role-based planning-ситуацию через obstacle chain.
    /// </summary>
    public sealed class DecisionPointNew
    {
        /// <summary>
        /// Создает role-based decision point из текущей chain.
        /// </summary>
        public DecisionPointNew(ObstacleChainNew chain)
        {
            Chain = chain;
        }

        public ObstacleChainNew Chain { get; }
    }
}
