namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofSwitchLane: смена линии с крыши — хомяк спускается на дорогу другой полосы.
    /// Аналог SwitchLaneStrategy, но после завершения HamsterOnRoof = false.
    /// </summary>
    public class RoofSwitchLaneStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.RoofSwitchLane;

        /// <summary>
        /// Пробует построить шаг RoofSwitchLane: поиск безопасного момента на целевой дорожной полосе → создание шага.
        /// </summary>
        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;

            if (problem == null || problem.Kind != ProblemKind.ThreatCollision)
            {
                rejectReason = "unsupported problem";
                return false;
            }

            var target = problem.SourceObstacle;

            // Найти первый безопасный момент для спуска на дорогу другой полосы
            if (!TryFindSafeFireShift(snapshot, target, out float fireWorldShift))
            {
                rejectReason = "no safe fire shift";
                return false;
            }

            float executeAtDistance = target.DistanceToHamster - fireWorldShift;
            if (executeAtDistance < 0f)
                executeAtDistance = 0f;

            float completionWorldShift = fireWorldShift + BotConsts.SwitchLaneDecisionTravel;

            step = new BranchStep(
                BotAction.RoofSwitchLane,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                energyCost: 0,
                $"RoofSwitchLane avoid {target.Type}");
            rejectReason = null;
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            completionSnapshot.HamsterOnBottom = !completionSnapshot.HamsterOnBottom;
            completionSnapshot.HamsterOnRoof = false;

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = PlannerState.FromSnapshot(completionSnapshot),
                DebugReason = step.Reason
            };
        }

        /// <summary>
        /// Ищет первый безопасный момент для спуска с крыши на дорогу другой полосы.
        /// Логика аналогична SwitchLaneStrategy: проверяет угрозы на целевой полосе,
        /// которые ещё не прошли мимо хомяка.
        /// </summary>
        private static bool TryFindSafeFireShift(
            BotSceneSnapshot snapshot,
            ObstacleInfo sourceTarget,
            out float fireWorldShift)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            bool targetLaneBottom = !snapshot.HamsterOnBottom;
            float sourceDeadlineShift = sourceTarget.DistanceToHamster;

            // Найти максимальный shift, при котором все угрозы на целевой полосе уже пройдут
            float fireShift = 0f;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != targetLaneBottom)
                    continue;

                if (obs.LeftX >= sourceTarget.LeftX)
                    continue;

                float clearShift = obs.RightX - hamsterLeftX;
                if (clearShift > fireShift)
                    fireShift = clearShift;
            }

            if (fireShift <= sourceDeadlineShift)
            {
                fireWorldShift = fireShift;
                return true;
            }

            fireWorldShift = 0f;
            return false;
        }
    }
}
