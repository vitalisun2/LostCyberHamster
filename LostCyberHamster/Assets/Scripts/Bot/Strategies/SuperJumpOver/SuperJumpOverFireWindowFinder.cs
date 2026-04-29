using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
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
        private readonly JumpFireWindowDiagnostics _diagnostics;

        public SuperJumpOverFireWindowFinder()
        {
            _searchWindowPolicy = new GroundJumpSearchWindowPolicy();
            _outcomeMatcher = new JumpOutcomeMatcher(
                HamsterStateEnum.SuperJumpOver,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump);
            _diagnostics = new JumpFireWindowDiagnostics(null);
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
                _diagnostics.LogWindow("NO_WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, superJumpTravel, firstFireShift, lastFireShift);
                fireShift = 0f;
                return false;
            }

            // Логируем окно и ищем точку fire shift с exact outcome.
            _diagnostics.LogWindow("WINDOW", planningState.Hamster, targetObstacle, targetObstacleIndex, superJumpTravel, firstFireShift, lastFireShift);

            HamsterSnapshot hamster = planningState.Hamster;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);

            bool selected = JumpFireShiftScanner.TrySelectFireShift(
                firstFireShift,
                lastFireShift,
                _preferLatestFireShift,
                candidateFireShift => _outcomeMatcher.IsExactOutcomeAtShift(
                    hamster,
                    baseObstacles,
                    shiftedObstacles,
                    candidateFireShift,
                    superJumpTravel,
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
                superJumpTravel);
            return true;
        }
    }
}