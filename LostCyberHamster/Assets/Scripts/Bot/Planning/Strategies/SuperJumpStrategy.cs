using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия SuperJump: перепрыгнуть препятствие типа bigAlive, когда обычный Jump невозможен.
    /// Применяется только для bigAlive на дороге (не с крыши — SuperRoofJump отдельная задача).
    /// </summary>
    internal class SuperJumpStrategy : IActionStrategy
    {
        private const float SuperJumpFireDist = 1.5f;

        public BotAction Action => BotAction.SuperJump;

        /// <summary>
        /// Пробует построить шаг SuperJump: валидация условий → расчёт тайминга → проверка зоны приземления.
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

            if (target.Type != ObstacleTypeEnum.bigAlive)
            {
                rejectReason = "not bigAlive";
                return false;
            }

            if (snapshot.HamsterOnRoof)
            {
                rejectReason = "on roof — use SuperRoofJump strategy";
                return false;
            }

            if (snapshot.Energy < BotConsts.SuperJumpEnergyCost)
            {
                rejectReason = "not enough energy";
                return false;
            }

            float executeAtDistance = SuperJumpFireDist;
            if (executeAtDistance > target.DistanceToHamster)
                executeAtDistance = target.DistanceToHamster;

            float fireWorldShift = target.DistanceToHamster - executeAtDistance;
            float completionWorldShift = fireWorldShift + BotConsts.SuperJumpLandingOffset;

            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, completionWorldShift);
            ApplySuperJumpEffects(completionSnapshot, target.StableId);

            if (!IsLaneClearAtCompletion(completionSnapshot, target.StableId))
            {
                rejectReason = "landing zone blocked";
                return false;
            }

            step = new BranchStep(
                BotAction.SuperJump,
                target,
                executeAtDistance,
                fireWorldShift,
                completionWorldShift,
                BotConsts.SuperJumpEnergyCost,
                $"SuperJump over {target.Type}");
            return true;
        }

        public StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld)
        {
            var completionSnapshot = projectedWorld.ProjectSnapshot(snapshot, step.CompletionWorldShift);
            ApplySuperJumpEffects(completionSnapshot, step.TargetObstacle.StableId);

            if (!IsLaneClearAtCompletion(completionSnapshot, step.TargetObstacle.StableId))
            {
                DebugManager.DiagLog(
                    $"[Bot PROJ] UNSAFE SuperJump landing overlap" +
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

        private static void ApplySuperJumpEffects(BotSceneSnapshot snapshot, int targetStableId)
        {
            snapshot.HamsterOnRoof = false;
            snapshot.Energy -= BotConsts.SuperJumpEnergyCost;
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
