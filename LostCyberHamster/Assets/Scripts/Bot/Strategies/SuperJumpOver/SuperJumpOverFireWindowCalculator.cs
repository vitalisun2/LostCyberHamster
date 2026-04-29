using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Ищет fire shift для super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverFireWindowCalculator : IJumpScheduledFireShiftValidator
    {
        private readonly IJumpSearchWindowPolicy _searchWindowPolicy;
        private readonly JumpOutcomeMatcher _outcomeMatcher;
        private readonly JumpFireWindowDiagnostics _diagnostics;

        public SuperJumpOverFireWindowCalculator()
        {
            _searchWindowPolicy = new GroundJumpSearchWindowPolicy();
            _outcomeMatcher = new JumpOutcomeMatcher(
                HamsterStateEnum.SuperJumpOver,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump);
            _diagnostics = new JumpFireWindowDiagnostics(null);
        }

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireShift)
        {
            return TryFindFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                superJumpTravel,
                preferLatestFireShift: false,
                out fireShift);
        }

        public bool IsScheduledFireShiftStillValid(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            PlannedAction action,
            float validationEpsilon)
        {
            if (planningState == null || projectedWorldSnapshot == null || targetObstacle == null || action == null)
                return false;

            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            if (!JumpScheduledFireShift.TryGetRemaining(projectedWorldSnapshot, targetObstacle, action, out float fireShift))
                return false;

            if (fireShift < firstFireShift - validationEpsilon || fireShift > lastFireShift + validationEpsilon)
                return false;

            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            return IsFeasibleFireShift(
                planningState.Hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                action.PostFireWorldShift,
                targetObstacleIndex);
        }

        private bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            bool preferLatestFireShift,
            out float fireShift)
        {
            Guard.NotNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)),
                (_searchWindowPolicy, nameof(_searchWindowPolicy)),
                (_outcomeMatcher, nameof(_outcomeMatcher)));

            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    actionTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                _diagnostics.LogWindow("NO_WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, actionTravel, firstFireShift, lastFireShift);
                fireShift = 0f;
                return false;
            }

            _diagnostics.LogWindow("WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, actionTravel, firstFireShift, lastFireShift);
            return TrySelectFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                actionTravel,
                firstFireShift,
                lastFireShift,
                preferLatestFireShift,
                out fireShift);
        }

        private bool TrySelectFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            float firstFireShift,
            float lastFireShift,
            bool preferLatestFireShift,
            out float fireShift)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);

            bool selected = JumpFireShiftScanner.TrySelectFireShift(
                firstFireShift,
                lastFireShift,
                preferLatestFireShift,
                candidateFireShift => IsFeasibleFireShift(
                    hamster,
                    baseObstacles,
                    shiftedObstacles,
                    candidateFireShift,
                    actionTravel,
                    targetObstacleIndex),
                out fireShift,
                out SafeInterval selectedInterval,
                out int exactIntervalCount);

            if (!selected)
            {
                _diagnostics.LogNoExactOutcomeInterval(targetObstacle, targetObstacleIndex, exactIntervalCount);
                return false;
            }

            _diagnostics.LogExactOutcomeSelection(targetObstacle, targetObstacleIndex, selectedInterval, fireShift);
            _diagnostics.LogResolvedOutcomeAtSelectedShift(
                _outcomeMatcher,
                hamster,
                baseObstacles,
                shiftedObstacles,
                targetObstacle,
                targetObstacleIndex,
                fireShift,
                actionTravel);
            return true;
        }

        private bool IsFeasibleFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex)
        {
            return _outcomeMatcher.IsExactOutcomeAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel,
                targetObstacleIndex);
        }
    }
}
