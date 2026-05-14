using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный roof jump-over action.
    /// </summary>
    internal sealed class RoofJumpOverRetainedActionValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly IRoofJumpOverPolicy _policy;
        private readonly RoofJumpOverFireWindowFinder _fireWindowFinder;

        public RoofJumpOverRetainedActionValidator(
            IRoofJumpOverPolicy policy,
            RoofJumpOverFireWindowFinder fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            DecisionPoint decisionPoint = context.DecisionPoint;
            ObstacleSnapshot targetObstacle = context.TargetObstacle;
            PlannedAction action = context.Action;

            // Проверяет обязательные данные retained action.
            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || targetObstacle == null
                || !action.ResultRoofSupportInstanceId.HasValue)
            {
                return false;
            }

            if (decisionPoint.Chain.FirstObstacle.InstanceId != targetObstacle.InstanceId)
                return false;

            if (!_policy.TryGetTravel(out RoofJumpOverTravel travel))
                return false;

            // Пересчитывает актуальное roof hazard window.
            if (!RoofJumpOverChainCalculator.TryCalculate(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out RoofJumpOverChainModel chainModel))
            {
                return false;
            }

            // Восстанавливает текущий fire shift сохранённого action.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            if (fireShift < chainModel.FirstFireShift - ValidationEpsilon
                || fireShift > chainModel.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            // Проверяет, что сохранённый fire shift всё ещё внутри окна и даёт тот же support.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                action.ResultRoofSupportInstanceId.Value,
                fireShift,
                travel);
        }

        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            float projectedTriggerX = action.TriggerX - projectionWorldShift;
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - projectedTriggerX;
                    return true;
                }
            }

            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}