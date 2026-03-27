using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Projection math для planner'а.
    /// Ничего не знает о семантике конкретных действий.
    /// </summary>
    public class ProjectedWorld
    {
        private const float PassedObstacleMargin = 0.4f;

        public BotSceneSnapshot ProjectSnapshot(BotSceneSnapshot source, float worldShift)
        {
            var projected = new BotSceneSnapshot();
            projected.CopyFrom(source);
            projected.SnapshotTime = source.SnapshotTime;

            float hamsterRightX = source.HamsterRightX;

            for (int i = 0; i < source.VisibleObjects.Count; i++)
            {
                var obs = source.VisibleObjects[i];

                float newLeftX = obs.LeftX - worldShift;
                float newRightX = obs.RightX - worldShift;
                float newCenterX = obs.CenterX - worldShift;

                if (newRightX < hamsterRightX - source.HamsterWidth - PassedObstacleMargin)
                    continue;

                float newDistance = newLeftX - hamsterRightX;

                projected.VisibleObjects.Add(new ObstacleInfo(
                    obs.Type,
                    obs.IsTopLane,
                    newLeftX,
                    newRightX,
                    newCenterX,
                    newDistance,
                    obs.Category,
                    obs.StableId));
            }

            return projected;
        }

        public static float GetHamsterLeftX(BotSceneSnapshot snapshot)
        {
            return snapshot.HamsterRightX - snapshot.HamsterWidth;
        }

        public static bool IsThreatType(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallAlive:
                    return true;
                default:
                    return false;
            }
        }
    }
}
