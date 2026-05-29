using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof
{
    /// <summary>
    /// Подбирает и подтверждает fire shift для roof-to-road jump-on target.
    /// </summary>
    internal sealed class JumpOnFromRoofFireWindowFinder
    {
        /// <summary>
        /// Политика runtime-различий конкретного варианта.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        public JumpOnFromRoofFireWindowFinder(IJumpOnFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Находит fire shift, который попадает в аналитическое окно и подтверждается roof-jump resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpOnFromRoofTravel travel,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            ObstacleSnapshot lastRoof,
            out JumpOnFromRoofWindowModel window,
            out float fireShift)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Вычисляет аналитическое окно.
            fireShift = 0f;
            if (!JumpOnFromRoofWindowCalculator.TryCalculate(
                    planningState.Hamster,
                    chain,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    lastRoof,
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
                window.TargetObstacleIndex,
                window.TargetObstacle.InstanceId);
        }

        /// <summary>
        /// Проверяет, что runtime resolver в заданный fire shift попадает в ожидаемый target.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnFromRoofTravel travel,
            int targetObstacleIndex,
            int targetObstacleInstanceId)
        {
            // Отсекает невалидные входные данные.
            if (hamster == null
                || baseObstacles == null
                || fireShift < 0f
                || travel.RoofJumpTravel <= 0f
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            // Получает outcome runtime resolver-а.
            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                travel);

            // Сравнивает outcome с ожидаемым target.
            bool isExpected = result.State == _policy.ExpectedJumpOnState
                && result.TargetIndex == targetObstacleIndex
                && result.TargetIndex >= 0
                && result.TargetIndex < baseObstacles.Count
                && baseObstacles[result.TargetIndex].InstanceId == targetObstacleInstanceId;
            return isExpected;
        }

        /// <summary>
        /// Проецирует мир в момент resolver-а и вызывает runtime roof-jump resolver.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnFromRoofTravel travel)
        {
            // Сдвигает obstacles в позицию runtime resolver-а.
            var obstaclesAtResolveShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(
                baseObstacles,
                travel.GetResolveFireShift(fireShift),
                obstaclesAtResolveShift);

            // Собирает context resolver-а относительно resolver-точки.
            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.RoofJumpTravel,
                travel.ResolveTravel);

            // Возвращает policy-specific outcome.
            return _policy.Resolve(obstaclesAtResolveShift, context);
        }
    }
}
