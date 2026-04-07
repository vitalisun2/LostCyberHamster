using Assets.Scripts.Common;

namespace Assets.Scripts.Bot
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
            ObstacleInfo target,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;

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

            float completionWorldShift = fireWorldShift + BotConsts.SwitchLaneDecisionTravel;

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

            if (!IsLaneClearAtCompletion(completionSnapshot))
            {
                return new StepProjectionResult
                {
                    IsSafe = false,
                    DebugReason = step.Reason
                };
            }

            return new StepProjectionResult
            {
                IsSafe = true,
                NextState = PlannerState.FromSnapshot(completionSnapshot),
                DebugReason = step.Reason
            };
        }

        /// <summary>
        /// Ищет первый безопасный момент для перестроения.
        /// Угрозы на целевой полосе блокируют switch, только если хомяк физически столкнётся
        /// с ними во время transition. Далёкие угрозы пропускаются — они будут обработаны
        /// на следующем шаге ветки (branching).
        /// Дедлайн — успеть нажать до source target; после fire лейн переключается мгновенно.
        /// </summary>
        private static bool TryFindSafeFireShift(
            BotSceneSnapshot snapshot,
            ObstacleInfo sourceTarget,
            out float fireWorldShift)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;
            bool targetLaneBottom = !snapshot.HamsterOnBottom;
            float sourceDeadlineShift = sourceTarget.DistanceToHamster;

            // Найти минимальный fireShift, при котором ни одна угроза на целевой полосе
            // не столкнётся с хомяком во время transition.
            // Итерируем до стабилизации: рост fireShift может приблизить далёкие угрозы.
            float fireShift = 0f;
            bool changed = true;

            while (changed)
            {
                changed = false;

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

                    // Пропустить угрозы, которые не достигнут хомяка за время transition
                    float gapAtFire = obs.LeftX - fireShift - hamsterRightX;
                    if (gapAtFire > BotConsts.SwitchLaneDecisionTravel)
                        continue;

                    float clearShift = obs.RightX - hamsterLeftX;
                    if (clearShift > fireShift)
                    {
                        fireShift = clearShift;
                        changed = true;
                    }
                }
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

        /// <summary>
        /// Проверяет, что на целевой полосе нет коллизий с хомяком в момент завершения перехода.
        /// </summary>
        private static bool IsLaneClearAtCompletion(BotSceneSnapshot snapshot)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (!ProjectedWorld.IsThreatType(obs.Type))
                    continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != snapshot.HamsterOnBottom)
                    continue;

                if (CollisionUtils.IsOverlap(hamsterLeftX, hamsterRightX, obs.LeftX, obs.RightX))
                    return false;
            }

            return true;
        }
    }
}
