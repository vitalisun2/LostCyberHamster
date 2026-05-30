using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный ground jump-over action.
    /// </summary>
    internal sealed class JumpOverRetainedActionValidator : IRetainedActionValidator
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly IJumpOverPolicy _policy;
        private readonly JumpOverFireWindowFinder _fireWindowFinder;

        public JumpOverRetainedActionValidator(
            IJumpOverPolicy policy,
            JumpOverFireWindowFinder fireWindowFinder)
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

            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || targetObstacle == null
                || action == null)
            {
                return false;
            }

            if (!CanStillExecute(planningState.Hamster, targetObstacle, action))
                return false;

            if (decisionPoint.Chain.FirstObstacle.InstanceId != targetObstacle.InstanceId)
                return false;

            if (!JumpOverChainCalculator.TryCalculate(
                    _policy,
                    planningState.Hamster,
                    decisionPoint.Chain,
                    action.PostFireWorldShift,
                    out JumpOverChainModel chainWindow))
            {
                return false;
            }

            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            if (fireShift < chainWindow.FirstFireShift - ValidationEpsilon
                || fireShift > chainWindow.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                JumpObstacleProjection.BuildBase(projectedWorldSnapshot),
                fireShift,
                action.PostFireWorldShift,
                chainWindow);
        }

        private bool CanStillExecute(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            PlannedAction action)
        {
            return hamster != null
                && !hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.Energy >= action.EnergyCost
                && targetObstacle.IsBottomLine == hamster.IsOnBottomLine
                && _policy.CanJumpOverObstacle(targetObstacle.ObstacleType);
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
