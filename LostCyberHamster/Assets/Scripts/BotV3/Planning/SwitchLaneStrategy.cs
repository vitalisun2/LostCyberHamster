namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Стратегия построения и проекции SwitchLane через первый безопасный момент перестроения.
    /// Планировщик ищет момент, после которого целевая полоса свободна от угроз ближе источника.
    /// Project() только вычисляет состояние мира после завершения шага.
    /// </summary>
    public class SwitchLaneStrategy : IActionStrategy
    {
        public BotAction Action => BotAction.SwitchLane;

        /// <summary>
        /// Пробует построить шаг SwitchLane: валидация проблемы → поиск безопасного момента → создание шага.
        /// </summary>
        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;

            // Валидация типа проблемы
            if (problem == null || problem.Kind != ProblemKind.ThreatCollision)
            {
                rejectReason = "unsupported problem";
                return false;
            }

            var target = problem.SourceObstacle;

            // Найти первый безопасный момент для перестроения
            if (!TryFindSafeFireShift(snapshot, target, out float fireWorldShift))
            {
                rejectReason = "no safe fire shift";
                return false;
            }

            // Создать шаг с рассчитанным таймингом
            float executeAtDistance = target.DistanceToHamster - fireWorldShift;
            if (executeAtDistance < 0f)
                executeAtDistance = 0f;

            float completionWorldShift = fireWorldShift + BotPhysicsConsts.SwitchLaneDecisionTravel;

            step = new BranchStep(
                BotAction.SwitchLane,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                energyCost: 0,
                $"SwitchLane avoid {target.Type}");
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
        /// Ищет первый безопасный момент для перестроения.
        /// Собирает угрозы на целевой полосе ближе источника, вычисляет момент,
        /// когда все они пройдут мимо хомяка, и проверяет что этот момент до дедлайна.
        /// Дедлайн — успеть нажать до source target; после fire лейн переключается мгновенно.
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

            // Проверить, что момент fire до дедлайна source target
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
