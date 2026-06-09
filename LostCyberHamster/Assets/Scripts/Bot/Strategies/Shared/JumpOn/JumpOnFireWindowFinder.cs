using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Подтверждает fire shift для role-based ground jump-on target.
    /// </summary>
    internal sealed class JumpOnFireWindowFinder
    {
        private readonly IJumpOnPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного jump-on policy.
        /// </summary>
        public JumpOnFireWindowFinder(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Находит fire shift для выбранного target и подтверждает его runtime resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            out JumpOnWindowModel window,
            out float fireShift)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)),
                (targetObstacle, nameof(targetObstacle)));

            // Вычисляет аналитическое окно.
            fireShift = 0f;
            if (!JumpOnWindowCalculator.TryCalculate(
                    planningState.Hamster,
                    chain,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    travel,
                    out window))
            {
                return false;
            }

            // Подтверждает fire shift через runtime resolver.
            fireShift = window.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                travel,
                window.TargetObstacleIndex);
        }

        /// <summary>
        /// Проверяет, что runtime resolver в заданный fire shift попадает в ожидаемый target.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnTravel travel,
            int targetObstacleIndex)
        {
            // Отсекает невалидные входные данные.
            if (hamster == null
                || baseObstacles == null
                || fireShift < 0f
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                travel);

            return result.State == _policy.ExpectedJumpOnState
                   && result.TargetIndex == targetObstacleIndex;
        }

        /// <summary>
        /// Проецирует мир в момент fire shift и вызывает runtime resolver.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnTravel travel)
        {
            // Отсекает невозможную дистанцию resolver-а.
            if (travel.ResolveTravel <= 0f)
                return new JumpResolveResult(hamster.HamsterState, -1);

            // Сдвигает obstacles в позицию runtime resolver-а.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(
                baseObstacles,
                travel.GetResolveFireShift(fireShift),
                obstaclesAtFireShift);

            // Собирает контекст resolver-а.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.ResolveTravel,
                travel.ResolveTravel,
                damageBigAliveWithoutYByReach: false);

            return _policy.Resolve(obstaclesAtFireShift, context);
        }
    }
}
