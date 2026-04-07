using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия SuperJumpOver: перепрыгивание bigAlive на дороге.
    /// Таблица стратегий уже отфильтровала применимые типы и контекст — здесь только бизнес-логика.
    /// </summary>
    internal class SuperJumpOverStrategy : IActionStrategy
    {
        private const float SuperJumpFireDist = 1.5f;

        private readonly float _landingOffset;

        public SuperJumpOverStrategy(float landingOffset)
        {
            _landingOffset = landingOffset;
        }

        public BotAction Action => BotAction.SuperJump;

        /// <summary>
        /// Пробует построить шаг SuperJumpOver: валидация энергии → расчёт тайминга → проверка зоны приземления.
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

            if (snapshot.Energy < BotConsts.SuperJumpEnergyCost)
            {
                rejectReason = "not enough energy";
                return false;
            }

            float executeAtDistance = SuperJumpFireDist;
            if (executeAtDistance > target.DistanceToHamster)
                executeAtDistance = target.DistanceToHamster;

            float fireWorldShift = target.DistanceToHamster - executeAtDistance;
            float completionWorldShift = fireWorldShift + _landingOffset;

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

        /// <summary>
        /// Возвращает true только для типов, которые приводят к повреждению при приземлении SuperJump.
        /// bigNotAlive / mediumNotAlive безопасны (результат → SuperJumpOnRoof → RoofRun).
        /// </summary>
        private static bool IsSuperJumpLandingUnsafe(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsLaneClearAtCompletion(BotSceneSnapshot snapshot, int excludeId)
        {
            float hamsterLeftX = ProjectedWorld.GetHamsterLeftX(snapshot);
            float hamsterRightX = snapshot.HamsterRightX;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obs = snapshot.VisibleObjects[i];
                if (obs.StableId == excludeId) continue;
                if (!IsSuperJumpLandingUnsafe(obs.Type)) continue;

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
