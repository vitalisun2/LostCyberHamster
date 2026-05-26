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
        /// <summary>
        /// Число итераций бинарного поиска раннего fire shift.
        /// </summary>
        private const int EarliestFireShiftSearchIterations = 10;

        /// <summary>
        /// Доля валидного окна, на которую запуск смещается внутрь при obstacle перед target.
        /// </summary>
        private const float PreTargetObstacleWindowOffsetRatio = 0.2f;

        /// <summary>
        /// Политика runtime-различий конкретного jump-on варианта.
        /// </summary>
        private readonly IJumpOnPolicy _policy;

        public JumpOnFireWindowFinder(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Находит fire shift, который попадает в аналитическое окно и подтверждается runtime resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpOnTravel travel,
            out JumpOnWindowModel window,
            out float fireShift)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Вычисляет аналитическое окно.
            fireShift = 0f;
            if (!JumpOnWindowCalculator.TryCalculate(
                    planningState.Hamster,
                    chain,
                    travel.ResolveTravel,
                    out window))
            {
                return false;
            }

            // Подтверждает fire shift через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            bool hasExpectedOutcome = TryFindResolverValidFireShift(
                planningState.Hamster,
                baseObstacles,
                window,
                travel,
                out fireShift);

            return hasExpectedOutcome;
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

            // Получает outcome runtime resolver-а.
            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                travel);

            // Сравнивает outcome с ожидаемым target.
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

            // Возвращает policy-specific outcome.
            return _policy.Resolve(obstaclesAtFireShift, context);
        }

        /// <summary>
        /// Ищет ранний fire shift, который runtime resolver подтверждает как jump-on по target.
        /// </summary>
        private bool TryFindResolverValidFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            JumpOnWindowModel window,
            JumpOnTravel travel,
            out float fireShift)
        {
            // Проверяет левую границу окна.
            fireShift = window.FirstFireShift;
            JumpResolveResult firstOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                travel);
            if (IsExpectedOutcome(firstOutcome, window.TargetObstacleIndex))
            {
                ApplyPreTargetOffset(
                    hamster,
                    baseObstacles,
                    window,
                    travel,
                    ref fireShift);
                return true;
            }

            // Проверяет правую границу окна.
            float rightFireShift = window.LastFireShift;
            JumpResolveResult rightOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                rightFireShift,
                travel);
            if (!IsExpectedOutcome(rightOutcome, window.TargetObstacleIndex))
            {
                fireShift = rightFireShift;
                return false;
            }

            // Ищет самую раннюю подтвержденную точку.
            float leftFireShift = window.FirstFireShift;
            for (int iteration = 0; iteration < EarliestFireShiftSearchIterations; iteration++)
            {
                float candidateFireShift = (leftFireShift + rightFireShift) * 0.5f;
                JumpResolveResult candidateOutcome = ResolveRuntimeOutcomeAtFireShift(
                    hamster,
                    baseObstacles,
                    candidateFireShift,
                    travel);

                if (IsExpectedOutcome(candidateOutcome, window.TargetObstacleIndex))
                {
                    rightFireShift = candidateFireShift;
                    continue;
                }

                leftFireShift = candidateFireShift;
            }

            // Смещает результат внутрь окна при pre-target obstacle.
            fireShift = rightFireShift;
            ApplyPreTargetOffset(
                hamster,
                baseObstacles,
                window,
                travel,
                ref fireShift);
            return true;
        }

        /// <summary>
        /// При наличии obstacle перед target немного смещает запуск внутрь валидного окна.
        /// </summary>
        private void ApplyPreTargetOffset(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            JumpOnWindowModel window,
            JumpOnTravel travel,
            ref float fireShift)
        {
            // Проверяет наличие obstacle перед target.
            if (window.TargetObstacleChainIndex <= 0)
                return;

            // Рассчитывает предпочтительный сдвиг внутрь окна.
            float preferredFireShift =
                fireShift
                + (window.LastFireShift - fireShift) * PreTargetObstacleWindowOffsetRatio;
            if (preferredFireShift <= fireShift)
                return;

            // Подтверждает предпочтительный сдвиг через resolver.
            JumpResolveResult preferredOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                preferredFireShift,
                travel);
            if (!IsExpectedOutcome(preferredOutcome, window.TargetObstacleIndex))
                return;

            fireShift = preferredFireShift;
        }

        /// <summary>
        /// Проверяет, что resolver outcome соответствует target obstacle.
        /// </summary>
        private bool IsExpectedOutcome(
            JumpResolveResult outcome,
            int targetObstacleIndex)
        {
            return outcome.State == _policy.ExpectedJumpOnState
                   && outcome.TargetIndex == targetObstacleIndex;
        }
    }
}
