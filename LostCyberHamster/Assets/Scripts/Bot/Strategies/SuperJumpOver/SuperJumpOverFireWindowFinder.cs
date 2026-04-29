using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Ищет fire shift для super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverFireWindowFinder
    {
        private const bool _preferLatestFireShift = true;

        private readonly IJumpSearchWindowPolicy _searchWindowPolicy;
        private readonly JumpOutcomeMatcher _outcomeMatcher;

        public SuperJumpOverFireWindowFinder()
        {
            _searchWindowPolicy = new GroundJumpSearchWindowPolicy();
            _outcomeMatcher = new JumpOutcomeMatcher(
                HamsterStateEnum.SuperJumpOver,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump);
        }

        /// <summary>
        /// Подбирает fire shift внутри допустимого окна для super jump-over.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireShift)
        {
            // Выбрасываем исключение, если null.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)),
                (_searchWindowPolicy, nameof(_searchWindowPolicy)),
                (_outcomeMatcher, nameof(_outcomeMatcher)));

            // Получаем физически допустимое окно запуска.
            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    superJumpTravel,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                fireShift = 0f;
                return false;
            }

            // Ищем точку fire shift с exact outcome.
            HamsterSnapshot hamster = planningState.Hamster;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            var exactOutcomeEvaluator = new SuperJumpOverExactOutcomeEvaluator(
                _outcomeMatcher,
                hamster,
                baseObstacles,
                shiftedObstacles,
                superJumpTravel,
                targetObstacleIndex);

            bool selected = JumpFireShiftScanner.TrySelectFireShift(
                firstFireShift,
                lastFireShift,
                _preferLatestFireShift,
                exactOutcomeEvaluator,
                out fireShift,
                out _,
                out _);

            return selected;
        }

        private sealed class SuperJumpOverExactOutcomeEvaluator : IJumpFireShiftExactOutcomeEvaluator
        {
            private readonly JumpOutcomeMatcher _outcomeMatcher;
            private readonly HamsterSnapshot _hamster;
            private readonly IReadOnlyList<JumpObstacleData> _baseObstacles;
            private readonly List<JumpObstacleData> _shiftedObstacles;
            private readonly float _superJumpTravel;
            private readonly int _targetObstacleIndex;

            public SuperJumpOverExactOutcomeEvaluator(
                JumpOutcomeMatcher outcomeMatcher,
                HamsterSnapshot hamster,
                IReadOnlyList<JumpObstacleData> baseObstacles,
                List<JumpObstacleData> shiftedObstacles,
                float superJumpTravel,
                int targetObstacleIndex)
            {
                _outcomeMatcher = outcomeMatcher;
                _hamster = hamster;
                _baseObstacles = baseObstacles;
                _shiftedObstacles = shiftedObstacles;
                _superJumpTravel = superJumpTravel;
                _targetObstacleIndex = targetObstacleIndex;
            }

            public bool IsExactOutcome(float fireShift)
            {
                return _outcomeMatcher.IsExactOutcomeAtShift(
                    _hamster,
                    _baseObstacles,
                    _shiftedObstacles,
                    fireShift,
                    _superJumpTravel,
                    _targetObstacleIndex);
            }
        }
    }
}