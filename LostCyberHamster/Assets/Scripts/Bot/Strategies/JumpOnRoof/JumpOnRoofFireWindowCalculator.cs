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

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Ищет fire shift для посадки на крышу.
    /// </summary>
    internal sealed class JumpOnRoofFireWindowCalculator : IJumpScheduledFireShiftValidator
    {
        private readonly IJumpSearchWindowPolicy _searchWindowPolicy;
        private readonly IPreFireSafetyPolicy _preFireSafetyPolicy;
        private readonly JumpOutcomeMatcher _outcomeMatcher;
        private readonly JumpFireWindowDiagnostics _diagnostics;

        public JumpOnRoofFireWindowCalculator()
        {
            _searchWindowPolicy = new RoofLandingSearchWindowPolicy();
            _preFireSafetyPolicy = new GroundContactPreFireSafetyPolicy();
            _outcomeMatcher = new JumpOutcomeMatcher(
                HamsterStateEnum.JumpOnRoof,
                damageBigAliveWithoutYByReach: true,
                JumpOutcomeResolver.ResolveJump);
            _diagnostics = new JumpFireWindowDiagnostics("JumpOnRoof");
        }

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float jumpTravel,
            bool preferLatestFireShift,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)),
                (_searchWindowPolicy, nameof(_searchWindowPolicy)),
                (_preFireSafetyPolicy, nameof(_preFireSafetyPolicy)),
                (_outcomeMatcher, nameof(_outcomeMatcher)));

            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    jumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                _diagnostics.LogWindow("NO_WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, jumpTravel, firstFireShift, lastFireShift);
                fireShift = 0f;
                return false;
            }

            _diagnostics.LogWindow("WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, jumpTravel, firstFireShift, lastFireShift);
            return TrySelectFireShift(
                planningState,
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex,
                jumpTravel,
                firstFireShift,
                lastFireShift,
                preferLatestFireShift,
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
            var exactOutcomeEvaluator = new JumpOnRoofExactOutcomeEvaluator(
                _preFireSafetyPolicy,
                _outcomeMatcher,
                hamster,
                baseObstacles,
                shiftedObstacles,
                actionTravel,
                targetObstacleIndex);

            bool selected = JumpFireShiftScanner.TrySelectFireShift(
                firstFireShift,
                lastFireShift,
                preferLatestFireShift,
                exactOutcomeEvaluator,
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
            if (!_preFireSafetyPolicy.CanWaitUntilFire(hamster, baseObstacles, fireShift))
                return false;

            return _outcomeMatcher.IsExactOutcomeAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel,
                targetObstacleIndex);
        }

        private sealed class JumpOnRoofExactOutcomeEvaluator : IJumpFireShiftExactOutcomeEvaluator
        {
            private readonly IPreFireSafetyPolicy _preFireSafetyPolicy;
            private readonly JumpOutcomeMatcher _outcomeMatcher;
            private readonly HamsterSnapshot _hamster;
            private readonly IReadOnlyList<JumpObstacleData> _baseObstacles;
            private readonly List<JumpObstacleData> _shiftedObstacles;
            private readonly float _actionTravel;
            private readonly int _targetObstacleIndex;

            public JumpOnRoofExactOutcomeEvaluator(
                IPreFireSafetyPolicy preFireSafetyPolicy,
                JumpOutcomeMatcher outcomeMatcher,
                HamsterSnapshot hamster,
                IReadOnlyList<JumpObstacleData> baseObstacles,
                List<JumpObstacleData> shiftedObstacles,
                float actionTravel,
                int targetObstacleIndex)
            {
                _preFireSafetyPolicy = preFireSafetyPolicy;
                _outcomeMatcher = outcomeMatcher;
                _hamster = hamster;
                _baseObstacles = baseObstacles;
                _shiftedObstacles = shiftedObstacles;
                _actionTravel = actionTravel;
                _targetObstacleIndex = targetObstacleIndex;
            }

            public bool IsExactOutcome(float fireShift)
            {
                if (!_preFireSafetyPolicy.CanWaitUntilFire(_hamster, _baseObstacles, fireShift))
                    return false;

                return _outcomeMatcher.IsExactOutcomeAtShift(
                    _hamster,
                    _baseObstacles,
                    _shiftedObstacles,
                    fireShift,
                    _actionTravel,
                    _targetObstacleIndex);
            }
        }
    }
}
