using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия RoofJumpOver: перепрыгивание smallNotAliveRoadAndRoof, пока хомяк бежит по крыше.
    /// Аналог JumpOverStrategy, но хомяк остаётся на крыше после приземления.
    /// </summary>
    public class RoofJumpOverStrategy : IActionStrategy
    {
        private const float JumpFireDist = 1.5f;

        public BotAction Action => BotAction.RoofJumpOver;

        /// <summary>
        /// Пробует построить шаг RoofJumpOver: валидация энергии → расчёт тайминга → проверка зоны приземления на крыше.
        /// </summary>
        public bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason)
        {
            step = null;
            rejectReason = null;

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

            // Проверить, что зона приземления свободна на крыше
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, completionWorldShift);
            ApplyRoofJumpOverEffects(completionSnapshot, target.StableId);

            if (!IsRoofLaneClearAtCompletion(completionSnapshot, target.StableId))
            {
                rejectReason = "landing zone blocked on roof";
                return false;
            }

            step = new BranchStep(
                BotAction.RoofJumpOver,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                BotConsts.JumpEnergyCost,
                $"RoofJumpOver {target.Type}");
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            ApplyRoofJumpOverEffects(completionSnapshot, step.TargetObstacle.StableId);

            if (!IsRoofLaneClearAtCompletion(completionSnapshot, step.TargetObstacle.StableId))
            {
                DebugManager.DiagLog(
                    $"[Bot PROJ] UNSAFE RoofJumpOver landing overlap" +
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
        /// Эффекты RoofJumpOver: хомяк остаётся на крыше, препятствие удаляется из snapshot.
        /// </summary>
        private static void ApplyRoofJumpOverEffects(BotSceneSnapshot snapshot, int targetStableId)
        {
            snapshot.HamsterOnRoof = true;
            snapshot.Energy -= BotConsts.JumpEnergyCost;
            if (snapshot.Energy < 0)
                snapshot.Energy = 0;

            snapshot.VisibleObjects.RemoveAll(o => o.StableId == targetStableId);
        }

        /// <summary>
        /// Проверяет, нет ли угроз на той же линии в зоне приземления.
        /// </summary>
        private static bool IsRoofLaneClearAtCompletion(BotSceneSnapshot snapshot, int excludeId)
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
