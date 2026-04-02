using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия JumpOnRoof: запрыгивание на крышу bigNotAlive/mediumNotAlive.
    /// Таблица стратегий уже отфильтровала применимые типы — здесь только бизнес-логика.
    /// </summary>
    public class JumpOnRoofStrategy : IActionStrategy
    {
        private const float JumpFireDist = 1.5f;
        private readonly float _landingOffset;

        public JumpOnRoofStrategy(float jumpOnRoofLandingOffset = BotConsts.JumpOnRoofLandingOffsetFallback)
        {
            _landingOffset = jumpOnRoofLandingOffset;
        }

        public BotAction Action => BotAction.JumpOnRoof;

        /// <summary>
        /// Пробует построить шаг JumpOnRoof: валидация энергии → расчёт тайминга → проверка крыши.
        /// </summary>
        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;
            rejectReason = null;

            if (problem == null || problem.Kind != ProblemKind.ThreatCollision)
            {
                rejectReason = "unsupported problem";
                return false;
            }

            var target = problem.SourceObstacle;

            if (snapshot.Energy < BotConsts.JumpEnergyCost)
            {
                rejectReason = "not enough energy";
                return false;
            }

            // Рассчитать тайминг fire и completion
            float executeAtDistance = JumpFireDist;
            if (executeAtDistance > target.DistanceToHamster)
                executeAtDistance = target.DistanceToHamster;

            float fireWorldShift = target.DistanceToHamster - executeAtDistance;
            float completionWorldShift = fireWorldShift + _landingOffset;

            // Проверить, что на крыше нет smallNotAliveRoadAndRoof в точке приземления
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, completionWorldShift);
            ApplyJumpOnRoofEffects(completionSnapshot);

            if (!IsRoofClearAtLanding(completionSnapshot, target))
            {
                rejectReason = "roof has obstacle at landing point";
                return false;
            }

            step = new BranchStep(
                BotAction.JumpOnRoof,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                BotConsts.JumpEnergyCost,
                $"Jump on roof ({target.Type})");
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            ApplyJumpOnRoofEffects(completionSnapshot);

            if (!IsRoofClearAtLanding(completionSnapshot, step.TargetObstacle))
            {
                DebugManager.DiagLog(
                    $"[Bot PROJ] UNSAFE JumpOnRoof landing overlap" +
                    $" → worldShift={step.CompletionWorldShift:F2}");

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
        /// Эффекты запрыгивания на крышу: HamsterOnRoof=true, препятствие НЕ удаляется.
        /// </summary>
        private static void ApplyJumpOnRoofEffects(BotSceneSnapshot snapshot)
        {
            snapshot.HamsterOnRoof = true;
            snapshot.Energy -= BotConsts.JumpEnergyCost;
            if (snapshot.Energy < 0)
                snapshot.Energy = 0;
        }

        /// <summary>
        /// Проверяет, нет ли smallNotAliveRoadAndRoof на крыше в точке приземления.
        /// </summary>
        private static bool IsRoofClearAtLanding(BotSceneSnapshot snapshot, ObstacleInfo roof)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof) continue;
                if (obs.IsTopLane != roof.IsTopLane) continue;

                if (CollisionUtils.IsOverlap(
                    hamsterLeftX, hamsterRightX,
                    obs.LeftX, obs.RightX))
                    return false;
            }

            return true;
        }
    }
}
