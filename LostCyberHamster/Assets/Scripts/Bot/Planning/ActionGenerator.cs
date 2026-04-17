using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.Strategies;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class ActionGenerator
    {
        private readonly IReadOnlyList<IPlanningStrategy> _strategies;

        public ActionGenerator()
        {
            _strategies = new IPlanningStrategy[]
            {
                new SwitchLaneStrategy()
            };
        }

        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            var plannedActions = new List<PlannedAction>();
            if (planningState == null || worldSnapshot == null)
                return plannedActions;

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            ObstacleSnapshot targetObstacle = null;
            int targetObstacleIndex = -1;

            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                    continue;

                if (!IsThreat(obstacle.ObstacleType))
                    continue;

                if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                    continue;

                targetObstacle = obstacle;
                targetObstacleIndex = obstacleIndex;
                break;
            }

            if (targetObstacle == null)
                return plannedActions;

            for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
            {
                if (_strategies[strategyIndex].TryGenerate(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    out PlannedAction action))
                {
                    plannedActions.Add(action);
                }
            }

            return plannedActions;
        }

        private static bool IsThreat(ObstacleTypeEnum obstacleType)
        {
            return obstacleType == ObstacleTypeEnum.smallAlive
                || obstacleType == ObstacleTypeEnum.bigAlive
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoad
                || obstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof
                || obstacleType == ObstacleTypeEnum.bigNotAlive
                || obstacleType == ObstacleTypeEnum.mediumNotAlive;
        }
    }
}
