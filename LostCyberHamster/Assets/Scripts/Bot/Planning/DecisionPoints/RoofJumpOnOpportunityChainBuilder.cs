using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Строит off-line opportunity-chain для сценария SwitchLane -> JumpOnRoof -> JumpOnFromRoof.
    /// </summary>
    internal static class RoofJumpOnOpportunityChainBuilder
    {
        /// <summary>
        /// Возвращает chain, начинающуюся с off-line roof obstacle и ведущую к roof jump-on target
        /// после всей passive roof-chain на целевой линии.
        /// </summary>
        public static bool TryBuildOpportunityChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            float maxRoofLeftX,
            float maxTargetLeftX,
            out ObstacleChain opportunityChain)
        {
            opportunityChain = null;

            if (planningState == null || planningState.Hamster == null || worldSnapshot == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            for (int obstacleIndex = firstObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine == hamster.IsOnBottomLine)
                    continue;

                if (obstacle.LeftX > maxRoofLeftX)
                    return false;

                if (!ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                    continue;

                if (!TryBuildFromRoof(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        obstacleIndex,
                        maxTargetLeftX,
                        out opportunityChain))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Строит opportunity-chain от найденной будущей крыши до road target после passive roof path.
        /// </summary>
        private static bool TryBuildFromRoof(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot roofObstacle,
            int roofObstacleIndex,
            float maxTargetLeftX,
            out ObstacleChain opportunityChain)
        {
            opportunityChain = null;

            PlanningState futureRoofState = BuildFutureRoofRunState(
                planningState,
                roofObstacle,
                roofObstacleIndex);

            if (!JumpOnFromRoofTargetChainBuilder.TryBuildTargetChain(
                    futureRoofState,
                    worldSnapshot,
                    maxTargetLeftX,
                    out ObstacleChain roadTargetChain,
                    out _))
            {
                return false;
            }

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
        /// Создаёт projected state после будущей посадки на крышу, чтобы переиспользовать roof-run target-chain builder.
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
    }
}
