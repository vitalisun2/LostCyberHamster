namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Цепочка шагов бота.
    /// Всегда содержит FirstStep.
    /// SecondStep = null для одношаговых цепочек.
    /// </summary>
    public class ChainCandidate
    {
        public ChainStep FirstStep;
        public ChainStep SecondStep;
        public bool SecondStepUsesProjectedCoordinates;
        public int TotalEnergyCost;
        public int TotalProfitScore;
        public DecisionRank BestRank;
    }
}