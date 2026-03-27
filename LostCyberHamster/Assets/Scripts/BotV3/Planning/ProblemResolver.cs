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
        public float DecisionWorldShift { get; }
        public string Reason { get; }

        public ProblemDescriptor(
            ProblemKind kind,
            ObstacleInfo sourceObstacle,
            float decisionWorldShift,
            string reason)
        {
            Kind = kind;
            SourceObstacle = sourceObstacle;
            DecisionWorldShift = decisionWorldShift;
            Reason = reason;
        }
    }

    /// <summary>
    /// Находит следующий обязательный decision point в snapshot.
    /// Для threat-only этапа это первая same-lane угроза, которая при чистом run
    /// приводит к реальному overlap с хомяком.
    /// </summary>
    public class ProblemResolver
    {
        public ProblemDescriptor ResolveNext(BotSceneSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot) - BotPhysicsConsts.SafetyPadding;
            float hamsterRightX = snapshot.HamsterRightX + BotPhysicsConsts.SafetyPadding;

            ObstacleInfo nextThreat = default;
            float nextDecisionShift = float.PositiveInfinity;
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

                float decisionShift = obstacle.LeftX - hamsterRightX;
                if (decisionShift < 0f)
                    decisionShift = 0f;

                if (decisionShift < nextDecisionShift)
                {
                    nextThreat = obstacle;
                    nextDecisionShift = decisionShift;
                    foundThreat = true;
                }
            }

            if (!foundThreat)
                return null;

            return new ProblemDescriptor(
                ProblemKind.ThreatCollision,
                nextThreat,
                nextDecisionShift,
                $"Threat collision: {nextThreat.Type}");
        }
    }
}
