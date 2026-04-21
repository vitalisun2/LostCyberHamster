using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.Strategies;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Проверяет, можно ли сохранить пограничное committed-действие на новом snapshot мира.
    /// </summary>
    internal sealed class RetainedActionRevalidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();

        /// <summary>
        /// Возвращает true, если последнее retained-действие всё ещё безопасно и остаётся актуальным.
        /// </summary>
        public bool IsStillValid(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return false;

            // Сначала проецируем текущий мир в состояние прямо перед boundary-действием.
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return false;

            // Затем убеждаемся, что действие всё ещё направлено в текущий blocking decision.
            if (!TryFindActionTarget(projectedWorldSnapshot, action, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return false;

            if (!_decisionPointDetector.TryDetect(planningState, projectedWorldSnapshot, out DecisionPoint decisionPoint))
                return false;

            if (decisionPoint.Obstacle.InstanceId != targetObstacle.InstanceId)
                return false;

            // После этого делегируем семантическую проверку конкретному типу действия.
            return action.Kind switch
            {
                BotActionKind.Tap => IsScheduledTapStillValid(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    action),
                BotActionKind.Jump => IsScheduledOverStillValid(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    action,
                    HamsterStateEnum.JumpOver,
                    damageBigAliveWithoutYByReach: true,
                    JumpOutcomeResolver.ResolveJump),
                BotActionKind.SuperJump => IsScheduledOverStillValid(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    action,
                    HamsterStateEnum.SuperJumpOver,
                    damageBigAliveWithoutYByReach: false,
                    SuperJumpOutcomeResolver.ResolveSuperJump),
                _ => false
            };
        }

        private static bool IsScheduledTapStillValid(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action)
        {
            if (planningState == null
                || projectedWorldSnapshot == null
                || targetObstacle == null
                || action == null
                || action.Kind != BotActionKind.Tap)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (!action.TargetBottomLine.HasValue || action.TargetBottomLine.Value == hamster.IsOnBottomLine)
                return false;

            if (!SwitchLaneStrategy.CanSwitchLane(planningState, targetObstacle))
                return false;

            if (!SwitchLaneStrategy.TryGetLatestFireShift(hamster, targetObstacle, out float latestFireShift))
                return false;

            float fireShift = targetObstacle.LeftX - action.TriggerX;
            if (fireShift < 0f || fireShift > latestFireShift + ValidationEpsilon)
                return false;

            List<SafeInterval> safeIntervals = SwitchLaneStrategy.CollectSafeFireIntervals(
                projectedWorldSnapshot,
                hamster,
                action.TargetBottomLine.Value,
                latestFireShift);
            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval interval = safeIntervals[intervalIndex];
                if (fireShift >= interval.Start - ValidationEpsilon
                    && fireShift <= interval.End + ValidationEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsScheduledOverStillValid(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            PlannedAction action,
            HamsterStateEnum expectedState,
            bool damageBigAliveWithoutYByReach,
            ActionWindowFinder.ResolveDelegate resolver)
        {
            if (planningState == null
                || projectedWorldSnapshot == null
                || targetObstacle == null
                || action == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            float actionTravel = action.PostFireWorldShift;
                if (!ActionWindowFinder.TryGetSearchWindow(
                    hamster,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    actionTravel,
                    out float firstFireShift,
                    out float lastFireShift))
                return false;

            float fireShift = targetObstacle.LeftX - action.TriggerX;
            if (fireShift < firstFireShift - ValidationEpsilon || fireShift > lastFireShift + ValidationEpsilon)
                return false;

            List<JumpObstacleData> baseObstacles = ActionWindowFinder.BuildBaseObstacleData(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            return ActionWindowFinder.IsExactOverAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel,
                targetObstacleIndex,
                expectedState,
                damageBigAliveWithoutYByReach,
                resolver);
        }

        private static bool TryFindActionTarget(
            WorldSnapshot projectedWorldSnapshot,
            PlannedAction action,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;

            if (projectedWorldSnapshot == null || action == null)
                return false;

            if (action.TargetObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != action.TargetObstacleInstanceId.Value)
                        continue;

                    targetObstacle = obstacle;
                    targetObstacleIndex = obstacleIndex;
                    return true;
                }
            }

            if (action.TargetObstacleIndex < 0 || action.TargetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count)
                return false;

            targetObstacleIndex = action.TargetObstacleIndex;
            targetObstacle = projectedWorldSnapshot.Obstacles[targetObstacleIndex];
            return true;
        }
    }
}