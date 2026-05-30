using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит optional off-lane chain для сценария SwitchLane -> JumpOnRoof -> JumpOnFromRoof.
    /// </summary>
    internal sealed class RoofJumpOnTargetChainBuilder : IDecisionPointChainBuilder
    {
        /// <summary>
        /// Пытается построить optional roof jump-on target decision point.
        /// </summary>
        public bool TryBuild(
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (!context.HasValidInput)
                return false;

            // Ищет будущую off-line крышу, после которой есть road target.
            for (int obstacleIndex = context.FirstObstacleIndex; obstacleIndex < context.WorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = context.WorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= context.Hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine == context.Hamster.IsOnBottomLine)
                    continue;

                if (obstacle.LeftX > context.MaxFirstObstacleLeftX)
                    return false;

                if (!ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                    continue;

                if (!TryBuildFromRoof(
                        context,
                        obstacle,
                        obstacleIndex,
                        out ObstacleChain opportunityChain))
                {
                    continue;
                }

                decisionPoint = new DecisionPoint(
                    opportunityChain,
                    DecisionPointKind.RoofJumpOnTarget,
                    isDecisionRequired: false,
                    fireBeforeObstacle: FindFireBeforeObstacleOrNull(context));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Строит opportunity-chain от найденной будущей крыши до road target после passive roof path.
        /// </summary>
        private static bool TryBuildFromRoof(
            DecisionPointBuildContext context,
            ObstacleSnapshot roofObstacle,
            int roofObstacleIndex,
            out ObstacleChain opportunityChain)
        {
            // Проецирует planning state в состояние после будущей посадки на крышу.
            opportunityChain = null;
            PlanningState futureRoofState = BuildFutureRoofRunState(
                context.PlanningState,
                roofObstacle,
                roofObstacleIndex);

            // Строит road target-chain после будущей roof-run цепочки.
            if (!JumpOnFromRoofTargetChainComposer.TryBuildTargetChain(
                    futureRoofState,
                    context.WorldSnapshot,
                    context.MaxTargetLeftX,
                    out ObstacleChain roadTargetChain,
                    out _))
            {
                return false;
            }

            // Добавляет будущую крышу перед road target-chain.
            var obstacles = new List<ObstacleSnapshot>(roadTargetChain.Count + 1)
            {
                roofObstacle
            };
            var indices = new List<int>(roadTargetChain.Count + 1)
            {
                roofObstacleIndex
            };

            for (int chainIndex = 0; chainIndex < roadTargetChain.Count; chainIndex++)
            {
                obstacles.Add(roadTargetChain.Obstacles[chainIndex]);
                indices.Add(roadTargetChain.Indices[chainIndex]);
            }

            opportunityChain = new ObstacleChain(obstacles, indices);
            return true;
        }

        /// <summary>
        /// Создаёт projected state после будущей посадки на крышу.
        /// </summary>
        private static PlanningState BuildFutureRoofRunState(
            PlanningState planningState,
            ObstacleSnapshot roofObstacle,
            int roofObstacleIndex)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            HamsterSnapshot futureHamster = new(
                HamsterStateEnum.RoofRun,
                roofObstacle.IsBottomLine,
                isOnRoof: true,
                hamster.Energy,
                hamster.Lives,
                isShifting: false,
                roofObstacle.InstanceId,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.HamsterBottomY,
                hamster.HamsterTopY);

            return new PlanningState(
                futureHamster,
                roofObstacleIndex,
                planningState.ProjectionWorldShift);
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
