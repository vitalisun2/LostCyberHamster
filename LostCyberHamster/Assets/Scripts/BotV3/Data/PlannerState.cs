using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Состояние планировщика для проекции следующего шага
    /// без обращения к Unity-объектам.
    /// </summary>
    public class PlannerState
    {
        public bool HamsterOnBottom;
        public bool HamsterOnRoof;
        public float HamsterRightX;
        public float HamsterWidth;
        public int Energy;
        public int Lives;
        public List<ObstacleInfo> RemainingObjects = new List<ObstacleInfo>();

        public PlannerState Clone()
        {
            return new PlannerState
            {
                HamsterOnBottom = HamsterOnBottom,
                HamsterOnRoof = HamsterOnRoof,
                HamsterRightX = HamsterRightX,
                HamsterWidth = HamsterWidth,
                Energy = Energy,
                Lives = Lives,
                RemainingObjects = new List<ObstacleInfo>(RemainingObjects)
            };
        }

        public static PlannerState FromSnapshot(BotSceneSnapshot snapshot)
        {
            return new PlannerState
            {
                HamsterOnBottom = snapshot.HamsterOnBottom,
                HamsterOnRoof = snapshot.HamsterOnRoof,
                HamsterRightX = snapshot.HamsterRightX,
                HamsterWidth = snapshot.HamsterWidth,
                Energy = snapshot.Energy,
                Lives = snapshot.Lives,
                RemainingObjects = new List<ObstacleInfo>(snapshot.VisibleObjects)
            };
        }

        public BotSceneSnapshot ToSnapshot()
        {
            return new BotSceneSnapshot
            {
                HamsterOnBottom = HamsterOnBottom,
                HamsterOnRoof = HamsterOnRoof,
                HamsterRightX = HamsterRightX,
                HamsterWidth = HamsterWidth,
                Energy = Energy,
                Lives = Lives,
                SnapshotTime = 0f,
                VisibleObjects = new List<ObstacleInfo>(RemainingObjects)
            };
        }
    }
}
