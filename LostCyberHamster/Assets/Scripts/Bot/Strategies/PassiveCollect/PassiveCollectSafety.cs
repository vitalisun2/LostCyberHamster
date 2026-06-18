using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.PassiveCollect
{
    /// <summary>
    /// Проверяет безопасность пассивного движения до collectable pickup.
    /// </summary>
    internal static class PassiveCollectSafety
    {
        private const float OverlapEpsilon = 0.0001f;

        /// <summary>
        /// Возвращает true, если до pickup нет same-lane danger contact.
        /// </summary>
        public static bool IsSafeUntilPickup(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot targetCollectible,
            float completionWorldShift)
        {
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null || targetCollectible == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanBlockPassiveCollect(hamster, obstacle, targetCollectible))
                    continue;

                if (TouchesHamsterBeforePickup(hamster, obstacle, completionWorldShift))
                    return false;
            }

            return true;
        }

        private static bool CanBlockPassiveCollect(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            ObstacleSnapshot targetCollectible)
        {
            if (obstacle == null
                || obstacle.IsRemovedInPlanning
                || obstacle.InstanceId == targetCollectible.InstanceId
                || obstacle.IsBottomLine != hamster.IsOnBottomLine
                || obstacle.RightX <= hamster.HamsterLeftX + OverlapEpsilon)
            {
                return false;
            }

            if (ObstacleClassifier.IsCollectible(obstacle.ObstacleType))
                return false;

            if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                return false;

            if (hamster.HamsterState == HamsterStateEnum.RoofRun
                || hamster.HamsterState == HamsterStateEnum.RunFromRoof)
            {
                return !ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
            }

            return hamster.HamsterState == HamsterStateEnum.Run;
        }

        private static bool TouchesHamsterBeforePickup(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            float completionWorldShift)
        {
            float contactShift = obstacle.LeftX - hamster.HamsterRightX;
            if (contactShift < 0f)
                contactShift = 0f;

            return contactShift <= completionWorldShift + OverlapEpsilon;
        }
    }
}
