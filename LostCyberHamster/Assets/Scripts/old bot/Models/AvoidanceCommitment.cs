namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Временное обязательство после avoidance-манёвра:
    /// не возвращаться на линию угрозы, пока сама угроза ещё активна.
    /// </summary>
    public readonly struct AvoidanceCommitment
    {
        public int ThreatStableId { get; }
        public bool ForbiddenLaneBottom { get; }

        public AvoidanceCommitment(int threatStableId, bool forbiddenLaneBottom)
        {
            ThreatStableId = threatStableId;
            ForbiddenLaneBottom = forbiddenLaneBottom;
        }

        public bool AppliesToTargetLane(bool targetLaneBottom)
        {
            return targetLaneBottom == ForbiddenLaneBottom;
        }

        public bool TryGetReleaseWorldShift(BotSceneSnapshot snapshot, out float releaseWorldShift)
        {
            releaseWorldShift = 0f;
            if (!TryFindActiveThreat(snapshot, out var threat))
                return false;

            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            releaseWorldShift = threat.RightX - hamsterLeftX;
            if (releaseWorldShift < 0f)
                releaseWorldShift = 0f;

            return true;
        }

        public bool IsStillActive(BotSceneSnapshot snapshot)
        {
            return TryFindActiveThreat(snapshot, out _);
        }

        private bool TryFindActiveThreat(BotSceneSnapshot snapshot, out ObstacleInfo threat)
        {
            threat = default;
            if (snapshot == null)
                return false;

            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId != ThreatStableId)
                    continue;

                if (!LaneMatches(obstacle))
                    continue;

                if (obstacle.RightX < hamsterLeftX)
                    return false;

                threat = obstacle;
                return true;
            }

            return false;
        }

        private bool LaneMatches(ObstacleInfo obstacle)
        {
            return !obstacle.IsTopLane == ForbiddenLaneBottom;
        }
    }
}
