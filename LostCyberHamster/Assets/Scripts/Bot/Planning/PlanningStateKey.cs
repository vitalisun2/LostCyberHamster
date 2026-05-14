using System;

namespace Assets.Scripts.Bot.Planning
{
    internal readonly struct PlanningStateKey : IEquatable<PlanningStateKey>
    {
        private const float ProjectionBucketSize = 0.05f;

        private PlanningStateKey(
            int nextObstacleIndex,
            int hamsterState,
            bool isOnBottomLine,
            bool isOnRoof,
            int roofSupportInstanceId,
            int energy,
            int lives,
            bool isShifting,
            int projectionBucket)
        {
            NextObstacleIndex = nextObstacleIndex;
            HamsterState = hamsterState;
            IsOnBottomLine = isOnBottomLine;
            IsOnRoof = isOnRoof;
            RoofSupportInstanceId = roofSupportInstanceId;
            Energy = energy;
            Lives = lives;
            IsShifting = isShifting;
            ProjectionBucket = projectionBucket;
        }

        private int NextObstacleIndex { get; }
        private int HamsterState { get; }
        private bool IsOnBottomLine { get; }
        private bool IsOnRoof { get; }
        private int RoofSupportInstanceId { get; }
        private int Energy { get; }
        private int Lives { get; }
        private bool IsShifting { get; }
        private int ProjectionBucket { get; }

        public static PlanningStateKey FromState(PlanningState planningState)
        {
            int projectionBucket = (int)Math.Round(
                planningState.ProjectionWorldShift / ProjectionBucketSize,
                MidpointRounding.AwayFromZero);
            return new PlanningStateKey(
                planningState.NextObstacleIndex,
                (int)planningState.Hamster.HamsterState,
                planningState.Hamster.IsOnBottomLine,
                planningState.Hamster.IsOnRoof,
                planningState.Hamster.RoofSupportInstanceId ?? -1,
                planningState.Hamster.Energy,
                planningState.Hamster.Lives,
                planningState.Hamster.IsShifting,
                projectionBucket);
        }

        public bool Equals(PlanningStateKey other)
        {
            return NextObstacleIndex == other.NextObstacleIndex
                && HamsterState == other.HamsterState
                && IsOnBottomLine == other.IsOnBottomLine
                && IsOnRoof == other.IsOnRoof
                && RoofSupportInstanceId == other.RoofSupportInstanceId
                && Energy == other.Energy
                && Lives == other.Lives
                && IsShifting == other.IsShifting
                && ProjectionBucket == other.ProjectionBucket;
        }

        public override bool Equals(object obj)
        {
            return obj is PlanningStateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = NextObstacleIndex;
                hash = (hash * 397) ^ HamsterState;
                hash = (hash * 397) ^ (IsOnBottomLine ? 1 : 0);
                hash = (hash * 397) ^ (IsOnRoof ? 1 : 0);
                hash = (hash * 397) ^ RoofSupportInstanceId;
                hash = (hash * 397) ^ Energy;
                hash = (hash * 397) ^ Lives;
                hash = (hash * 397) ^ (IsShifting ? 1 : 0);
                hash = (hash * 397) ^ ProjectionBucket;
                return hash;
            }
        }
    }
}
