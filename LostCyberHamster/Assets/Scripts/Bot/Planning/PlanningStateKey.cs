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
            int energy,
            int lives,
            bool isDamaged,
            bool isShifting,
            int projectionBucket)
        {
            NextObstacleIndex = nextObstacleIndex;
            HamsterState = hamsterState;
            IsOnBottomLine = isOnBottomLine;
            IsOnRoof = isOnRoof;
            Energy = energy;
            Lives = lives;
            IsDamaged = isDamaged;
            IsShifting = isShifting;
            ProjectionBucket = projectionBucket;
        }

        private int NextObstacleIndex { get; }
        private int HamsterState { get; }
        private bool IsOnBottomLine { get; }
        private bool IsOnRoof { get; }
        private int Energy { get; }
        private int Lives { get; }
        private bool IsDamaged { get; }
        private bool IsShifting { get; }
        private int ProjectionBucket { get; }

        public static PlanningStateKey FromState(PlanningState planningState)
        {
            int projectionBucket = (int)Math.Round(
                planningState.ProjectionWorldShift / ProjectionBucketSize,
                MidpointRounding.AwayFromZero);
            return new PlanningStateKey(
                planningState.NextObstacleIndex,
                (int)planningState.RuntimeState.HamsterState,
                planningState.RuntimeState.IsOnBottomLine,
                planningState.RuntimeState.IsOnRoof,
                planningState.RuntimeState.Energy,
                planningState.RuntimeState.Lives,
                planningState.RuntimeState.IsDamaged,
                planningState.RuntimeState.IsShifting,
                projectionBucket);
        }

        public bool Equals(PlanningStateKey other)
        {
            return NextObstacleIndex == other.NextObstacleIndex
                && HamsterState == other.HamsterState
                && IsOnBottomLine == other.IsOnBottomLine
                && IsOnRoof == other.IsOnRoof
                && Energy == other.Energy
                && Lives == other.Lives
                && IsDamaged == other.IsDamaged
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
                hash = (hash * 397) ^ Energy;
                hash = (hash * 397) ^ Lives;
                hash = (hash * 397) ^ (IsDamaged ? 1 : 0);
                hash = (hash * 397) ^ (IsShifting ? 1 : 0);
                hash = (hash * 397) ^ ProjectionBucket;
                return hash;
            }
        }
    }
}