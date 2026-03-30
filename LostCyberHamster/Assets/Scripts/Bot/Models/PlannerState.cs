using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Состояние планировщика для проекции следующего шага
    /// без обращения к Unity-объектам.
    /// </summary>
    public class PlannerState : BotStateBase
    {
        public List<ObstacleInfo> RemainingObjects = new List<ObstacleInfo>();

        public PlannerState Clone()
        {
            var clone = new PlannerState();
            clone.CopyFrom(this);
            clone.RemainingObjects = new List<ObstacleInfo>(RemainingObjects);
            return clone;
        }

        public static PlannerState FromSnapshot(BotSceneSnapshot snapshot)
        {
            var state = new PlannerState();
            state.CopyFrom(snapshot);
            state.RemainingObjects = new List<ObstacleInfo>(snapshot.VisibleObjects);
            return state;
        }

        public BotSceneSnapshot ToSnapshot()
        {
            var snapshot = new BotSceneSnapshot();
            snapshot.CopyFrom(this);
            snapshot.SnapshotTime = 0f;
            snapshot.VisibleObjects = new List<ObstacleInfo>(RemainingObjects);
            return snapshot;
        }
    }
}
