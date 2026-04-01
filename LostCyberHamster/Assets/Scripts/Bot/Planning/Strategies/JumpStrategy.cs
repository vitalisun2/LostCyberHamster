using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия JumpOver: перепрыгивание small препятствий (smallNotAliveRoad, smallNotAliveRoadAndRoof).
    /// Таблица стратегий уже отфильтровала применимые типы — здесь только бизнес-логика.
    /// </summary>
    public class JumpOverStrategy : IActionStrategy
    {
        private const float JumpFireDist = 1.5f;

        public BotAction Action => BotAction.JumpOver;

        /// <summary>
        /// Пробует построить шаг JumpOver: валидация энергии → расчёт тайминга → проверка зоны приземления.
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
            float completionWorldShift = fireWorldShift + BotConsts.JumpLandingOffset;

            // Проверить, что зона приземления свободна
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, completionWorldShift);
            ApplyJumpEffects(completionSnapshot, target.StableId);

            if (!IsLaneClearAtCompletion(completionSnapshot, target.StableId))
            {
                rejectReason = "landing zone blocked";
                return false;
            }

            // Создать шаг
            step = new BranchStep(
                BotAction.JumpOver,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                BotConsts.JumpEnergyCost,
                $"Jump over {target.Type}");
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            ApplyJumpEffects(completionSnapshot, step.TargetObstacle.StableId);

            if (!IsLaneClearAtCompletion(completionSnapshot, step.TargetObstacle.StableId))
            {
                DebugManager.DiagLog(
                    $"[Bot PROJ] UNSAFE Jump landing overlap" +
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

        private static void ApplyJumpEffects(BotSceneSnapshot snapshot, int targetStableId)
        {
            snapshot.HamsterOnRoof = false;
            snapshot.Energy -= BotConsts.JumpEnergyCost;
            if (snapshot.Energy < 0)
                snapshot.Energy = 0;

            snapshot.VisibleObjects.RemoveAll(o => o.StableId == targetStableId);
        }

        private static bool IsLaneClearAtCompletion(BotSceneSnapshot snapshot, int excludeId)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == excludeId) continue;
                if (!ProjectedWorld.IsThreatType(obs.Type)) continue;

                bool obsOnBottom = !obs.IsTopLane;
                if (obsOnBottom != snapshot.HamsterOnBottom) continue;

                if (CollisionUtils.IsOverlap(
                    hamsterLeftX,
                    hamsterRightX,
                    obs.LeftX,
                    obs.RightX))
                    return false;
            }

            return true;
        }
    }
}
