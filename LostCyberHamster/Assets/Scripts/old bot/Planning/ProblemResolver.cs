namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Находит ближайшую same-lane угрозу, требующую следующего обязательного решения.
    /// </summary>
    public class ProblemResolver
    {
        public bool TryResolveNextThreat(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            out ObstacleInfo nextThreat)
        {
            nextThreat = default;
            if (snapshot == null || classifier == null)
                return false;

            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            float distToThreat = float.PositiveInfinity;
            bool foundThreat = false;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (!classifier.IsThreat(obstacle, snapshot))
                    continue;
                if (!snapshot.IsOnSameLane(obstacle))
                    continue;
                if (obstacle.RightX < hamsterLeftX)
                    continue;

                float dist = obstacle.LeftX - hamsterRightX;
                if (dist < 0f)
                    dist = 0f;

                if (dist < distToThreat)
                {
                    nextThreat = obstacle;
                    distToThreat = dist;
                    foundThreat = true;
                }
            }

            return foundThreat;
        }
    }
}
