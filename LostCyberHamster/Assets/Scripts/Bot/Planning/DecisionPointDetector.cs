using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class DecisionPointDetector
    {
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            decisionPoint = null;

            if (planningState == null || worldSnapshot == null)
                return false;

            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                    continue;

                if (obstacle.IsBottomLine != planningState.IsOnBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                decisionPoint = new DecisionPoint(
                    DecisionPointKind.BlockingGroundObstacle,
                    obstacle,
                    obstacleIndex);
                return true;
            }

            return false;
        }
    }
}
