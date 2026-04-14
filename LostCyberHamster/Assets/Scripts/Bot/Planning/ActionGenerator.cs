using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.Strategies;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class ActionGenerator
    {
        private readonly IReadOnlyList<Assets.Scripts.Bot.Planning.Strategies.IPlanningStrategy> _strategies;

        public ActionGenerator()
        {
            _strategies = new Assets.Scripts.Bot.Planning.Strategies.IPlanningStrategy[]
            {
                new Assets.Scripts.Bot.Planning.Strategies.SwitchLaneStrategy()
            };
        }

        public IReadOnlyList<PlannedAction> Generate(PlanningState planningState, BotPerceptionSnapshot perceptionSnapshot)
        {
            var plannedActions = new List<PlannedAction>();

            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < perceptionSnapshot.VisibleObstacles.Count; obstacleIndex++)
            {
                VisibleObstacleSnapshot obstacle = perceptionSnapshot.VisibleObstacles[obstacleIndex];
                if (!IsThreat(obstacle.ObstacleType))
                    continue;

                if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                    continue;

                for (int strategyIndex = 0; strategyIndex < _strategies.Count; strategyIndex++)
                {
                    if (_strategies[strategyIndex].TryGenerate(planningState, perceptionSnapshot, obstacle, obstacleIndex, out PlannedAction action))
                        plannedActions.Add(action);
                }

                return plannedActions;
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
