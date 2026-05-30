namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит target-chain для jump-on при сходе с текущей крыши.
    /// </summary>
    internal sealed class JumpOnFromRoofTargetChainBuilder : IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить JumpOnFromRoof target decision point.
        /// </summary>
        public bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (!context.HasValidInput)
                return false;

            // Строит roof-exit target-chain в пределах заданного horizon.
            if (!JumpOnFromRoofTargetChainComposer.TryBuildTargetChain(
                    context.PlanningState,
                    context.WorldSnapshot,
                    context.MaxTargetLeftX,
                    out ObstacleChain targetChain,
                    out _))
            {
                return false;
            }

            decisionPoint = new DecisionPoint(
                targetChain,
                DecisionPointKind.JumpOnFromRoofTarget,
                isDecisionRequired: true);
            return true;
        }
    }
}
