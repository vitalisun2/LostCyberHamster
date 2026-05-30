using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит required current-lane chain, которая ведет к ground jump-on target.
    /// </summary>
    internal sealed class CurrentLaneGroundJumpOnTargetChainBuilder : IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить required ground jump-on target decision point.
        /// </summary>
        public bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (!context.HasValidInput)
                return false;

            // Строит source threat-chain текущей линии.
            if (!ThreatChainCollector.TryBuildNearestThreatChain(
                    context.PlanningState,
                    context.WorldSnapshot,
                    context.FirstObstacleIndex,
                    out ObstacleChain sourceChain))
            {
                return false;
            }

            // Расширяет source chain до первого ground jump-on target.
            if (!GroundJumpOnTargetChainComposer.TryBuildTargetChain(
                    context.PlanningState,
                    context.WorldSnapshot,
                    sourceChain,
                    context.MaxTargetLeftX,
                    out ObstacleChain targetChain))
            {
                return false;
            }

            decisionPoint = new DecisionPoint(
                targetChain,
                DecisionPointKind.GroundJumpOnTarget,
                isDecisionRequired: true);
            return true;
        }
    }

    /// <summary>
    /// Строит optional off-lane chain, которая ведет к ground jump-on target.
    /// </summary>
    internal sealed class OtherLaneGroundJumpOnTargetChainBuilder : IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить optional ground jump-on target decision point.
        /// </summary>
        public bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (!context.HasValidInput)
                return false;

            // Ищет off-line chain с jump-on target.
            PlanningState planningState = context.PlanningState;
            WorldSnapshot worldSnapshot = context.WorldSnapshot;
            for (int obstacleIndex = context.FirstObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= context.Hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine == planningState.IsOnBottomLine)
                    continue;

                if (obstacle.LeftX > context.MaxFirstObstacleLeftX)
                    return false;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (!ThreatChainCollector.TryBuildChainFromIndex(
                        planningState,
                        worldSnapshot,
                        obstacleIndex,
                        obstacle.IsBottomLine,
                        out ObstacleChain sourceChain))
                {
                    continue;
                }

                if (!GroundJumpOnTargetChainComposer.TryBuildTargetChain(
                        planningState,
                        worldSnapshot,
                        sourceChain,
                        context.MaxTargetLeftX,
                        out ObstacleChain targetChain))
                {
                    continue;
                }

                if (!targetChain.TryFindFirstGroundJumpOnTarget(
                        obstacle.IsBottomLine,
                        out ObstacleSnapshot targetObstacle,
                        out _,
                        out _))
                {
                    continue;
                }

                if (targetObstacle.LeftX > context.MaxTargetLeftX)
                    continue;

                decisionPoint = new DecisionPoint(
                    targetChain,
                    DecisionPointKind.GroundJumpOnTarget,
                    isDecisionRequired: false,
                    fireBeforeObstacle: FindFireBeforeObstacleOrNull(context));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает obstacle, до которого нужно успеть запустить optional action.
        /// </summary>
        private static ObstacleSnapshot FindFireBeforeObstacleOrNull(DecisionPointBuildContext context)
        {
            return ThreatChainCollector.TryFindFirstThreat(
                    context.PlanningState,
                    context.WorldSnapshot,
                    context.FirstObstacleIndex,
                    out int blockingThreatIndex)
                ? context.WorldSnapshot.Obstacles[blockingThreatIndex]
                : null;
        }
    }
}
