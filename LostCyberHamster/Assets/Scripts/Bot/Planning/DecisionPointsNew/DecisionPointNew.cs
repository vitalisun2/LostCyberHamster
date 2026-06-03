using System;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Описывает role-based planning-ситуацию через одну obstacle chain без сценарного kind.
    /// </summary>
    public sealed class DecisionPointNew
    {
        /// <summary>
        /// Создает role-based decision point из готовой chain.
        /// </summary>
        public DecisionPointNew(ObstacleChainNew chain)
        {
            Chain = chain ?? throw new ArgumentNullException(nameof(chain));
        }

        public ObstacleChainNew Chain { get; }
        public bool FocusBottomLine => Chain.FocusBottomLine;
    }
}
