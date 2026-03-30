namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Тип текущей задачи planner'а.
    /// На текущем этапе поддерживается только обязательная угроза столкновения.
    /// </summary>
    public enum ProblemKind
    {
        ThreatCollision
    }

    /// <summary>
    /// Явная проблема текущего decision point.
    /// Хранит объект-источник и момент, к которому без ответа возникнет collision.
    /// </summary>
    public sealed class ProblemDescriptor
    {
        public ProblemKind Kind { get; }
        public ObstacleInfo SourceObstacle { get; }
        public string Reason { get; }

        public ProblemDescriptor(
            ProblemKind kind,
            ObstacleInfo sourceObstacle,
            string reason)
        {
            Kind = kind;
            SourceObstacle = sourceObstacle;
            Reason = reason;
        }
    }

    /// <summary>
    /// Находит следующий обязательный decision point в snapshot.
    /// Для threat-only этапа это ближайшая same-lane угроза,
    /// создающая следующий обязательный decision point.
    /// </summary>
    public class ProblemResolver
    {
        /// <summary>
        /// Находит ближайшую same-lane угрозу, ещё не пройденную хомяком.
        /// </summary>
        public ProblemDescriptor ResolveNext(BotSceneSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            // Найти ближайшую same-lane угрозу
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            ObstacleInfo nextThreat = default;
            float distToThreat = float.PositiveInfinity;
            bool foundThreat = false;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat)
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

            if (!foundThreat)
                return null;

            // Создать дескриптор проблемы
            return new ProblemDescriptor(
                ProblemKind.ThreatCollision,
                nextThreat,
                $"Threat collision: {nextThreat.Type}");
        }
    }
}
