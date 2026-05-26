using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Подбирает и подтверждает fire shift для ground jump-on smallAlive.
    /// </summary>
    internal sealed class JumpOnFireWindowFinder
    {
        private readonly IJumpOnPolicy _policy;

        public JumpOnFireWindowFinder(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            float jumpTravel,
            out JumpOnWindowModel window,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            fireShift = 0f;
            if (!JumpOnWindowCalculator.TryCalculate(
                    planningState.Hamster,
                    chain,
                    jumpTravel,
                    out window))
            {
                return false;
            }

            fireShift = window.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            bool hasExpectedOutcome = CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                jumpTravel,
                window.TargetObstacleIndex);

            LogSelection(
                planningState,
                window,
                jumpTravel,
                fireShift,
                hasExpectedOutcome);

            return hasExpectedOutcome;
        }

        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            int targetObstacleIndex)
        {
            if (hamster == null || baseObstacles == null || fireShift < 0f || jumpTravel <= 0f)
                return false;

            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);

            return result.State == _policy.ExpectedJumpOnState
                   && result.TargetIndex == targetObstacleIndex;
        }

        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel)
        {
            _policy.GetResolveInput(
                fireShift,
                jumpTravel,
                out float resolveFireShift,
                out float resolveTravel);

            if (resolveTravel <= 0f)
                return new JumpResolveResult(hamster.HamsterState, -1);

            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, resolveFireShift, obstaclesAtFireShift);

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                resolveTravel,
                resolveTravel,
                damageBigAliveWithoutYByReach: false);

            return _policy.Resolve(obstaclesAtFireShift, context);
        }

        private void LogSelection(
            PlanningState planningState,
            JumpOnWindowModel window,
            float jumpTravel,
            float fireShift,
            bool hasExpectedOutcome)
        {
            ObstacleSnapshot targetObstacle = window.TargetObstacle;
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();

            DebugManager.DiagLogVerbose(
                $"[{_policy.LogTag} WINDOW] target={targetObstacle.ObstacleType} " +
                $"targetIndex={window.TargetObstacleIndex} " +
                $"travel={jumpTravel:F3} " +
                $"first={window.FirstFireShift:F3} last={window.LastFireShift:F3} selected={fireShift:F3} " +
                $"boundaryMargin={fireWindowBoundaryMargin:F3} " +
                $"projectedTriggerX={projectedTriggerX:F3} " +
                $"renderWorldX={renderWorldX:F3} targetLeft={targetObstacle.LeftX:F3} " +
                $"targetRight={targetObstacle.RightX:F3} " +
                $"hamsterCenter={planningState.Hamster.CenterX:F3} " +
                $"projection={planningState.ProjectionWorldShift:F3} " +
                $"expectedOutcome={hasExpectedOutcome}");
        }
    }
}
