using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnRoof
{
    /// <summary>
    /// Подбирает момент срабатывания прыжка для посадки бота на целевую крышу.
    /// </summary>
    internal sealed class JumpOnRoofFireWindowFinder
    {
        /// <summary>
        /// Количество итераций бинарного поиска ранней resolver-valid точки.
        /// </summary>
        private const int _earliestFireShiftSearchIterations = 10;

        /// <summary>
        /// Доля окна, на которую прыжок смещается позже при наличии препятствий перед крышей.
        /// </summary>
        private const float _preRoofObstacleWindowOffsetRatio = 0.2f;

        /// <summary>
        /// Политика конкретного типа прыжка на крышу.
        /// </summary>
        private readonly IJumpOnRoofPolicy _policy;

        public JumpOnRoofFireWindowFinder(IJumpOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Подбирает fire shift и target obstacle внутри допустимого окна для jump-on-roof chain.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            float jumpTravel,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out float firstFireShift,
            out float lastFireShift,
            out float fireShift)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;
            firstFireShift = 0f;
            lastFireShift = 0f;
            fireShift = 0f;

            // Проверяет обязательные входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Находит первую целевую крышу внутри chain.
            if (!chain.TryFindFirstRoof(out targetObstacle, out targetObstacleIndex, out int roofChainIndex))
            {
                return false;
            }

            // Вычисляет допустимое окно старта прыжка по выбранной крыше.
            if (!TryGetRoofLandingWindow(
                    planningState.Hamster,
                    chain,
                    targetObstacle,
                    roofChainIndex,
                    jumpTravel,
                    out firstFireShift,
                    out lastFireShift))
            {
                return false;
            }

            // Строит resolver input.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);

            // Ищет самый ранний resolver-valid fire shift.
            if (!TryFindEarliestResolverValidFireShift(
                    planningState.Hamster,
                    baseObstacles,
                    firstFireShift,
                    lastFireShift,
                    jumpTravel,
                    targetObstacleIndex,
                    hasPreRoofObstacle: roofChainIndex > 0,
                    out fireShift,
                    out JumpResolveResult selectedOutcome))
            {
                return false;
            }

            LogSelection(
                planningState,
                chain,
                targetObstacle,
                targetObstacleIndex,
                roofChainIndex,
                jumpTravel,
                firstFireShift,
                lastFireShift,
                fireShift,
                hasExpectedOutcome: true,
                selectedOutcome);

            return true;
        }

        /// <summary>
        /// Вычисляет допустимое окно fire shift для выбранной roof target внутри chain.
        /// </summary>
        internal static bool TryGetRoofLandingWindow(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot roofObstacle,
            int roofChainIndex,
            float jumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            // Сбрасывает результат и проверяет обязательные данные.
            firstFireShift = 0f;
            lastFireShift = 0f;

            if (hamster == null
                || chain == null
                || roofObstacle == null
                || roofChainIndex < 0
                || roofChainIndex >= chain.Count)
            {
                return false;
            }

            // Вычисляет левую границу fire-window.
            firstFireShift = CalculateFirstFireShift(hamster, roofObstacle, jumpTravel);

            // Вычисляет правую границу fire-window.
            lastFireShift = CalculateLastFireShift(
                hamster,
                chain,
                roofObstacle,
                roofChainIndex,
                jumpTravel);

            // Отступает внутрь от обеих границ fire-window единым jump margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            bool windowIsInFuture = lastFireShift > 0f;
            bool windowHasRange = firstFireShift < lastFireShift;
            return windowIsInFuture && windowHasRange;
        }

        /// <summary>
        /// Проверяет, что fire shift приводит к runtime outcome по целевой крыше.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            int targetObstacleIndex)
        {
            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);
            return result.State == _policy.ExpectedRoofState
                   && result.TargetIndex == targetObstacleIndex;
        }

        /// <summary>
        /// Проверяет, что fire shift сохраняет посадку на крышу по runtime resolver.
        /// </summary>
        internal bool CheckSafeRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            int targetObstacleIndex,
            ObstacleTypeEnum targetObstacleType)
        {
            // Получает runtime outcome для текущего retained fire shift.
            JumpResolveResult outcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);

            // Проверяет соответствие runtime outcome целевой крыше.
            return IsValidFireShift(targetObstacleIndex, outcome);
        }

        /// <summary>
        /// Получает runtime outcome для заданного fire shift.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel)
        {
            // Переводит planning fire shift в runtime-точку resolver'а.
            _policy.GetResolveInput(
                fireShift,
                jumpTravel,
                out float resolveFireShift,
                out float resolveTravel);

            // Строит obstacle snapshot на момент resolver'а.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, resolveFireShift, obstaclesAtFireShift);

            // Готовит runtime context для прыжка на крышу.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                resolveTravel,
                resolveTravel,
                damageBigAliveWithoutYByReach: _policy.DamageBigAliveWithoutYByReach);

            // Сверяет runtime outcome с целевой крышей.
            return _policy.Resolve(obstaclesAtFireShift, context);
        }

        /// <summary>
        /// Вычисляет левую границу fire-window по достижимости левого края целевой крыши.
        /// </summary>
        private static float CalculateFirstFireShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot roofObstacle,
            float jumpTravel)
        {
            // Находит момент, когда прыжок начинает доставать до крыши.
            // Отрицательное значение означает «крыша уже достижима прямо сейчас» —
            // клэмп до 0 нормализует к физическому минимуму «прыгнуть немедленно».
            float firstFireShift = Math.Max(0f, roofObstacle.LeftX - jumpTravel - hamster.HamsterRightX);
            return firstFireShift;
        }

        /// <summary>
        /// Вычисляет правую границу fire-window по obstacle chain и overlap с целевой крышей.
        /// </summary>
        private static float CalculateLastFireShift(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot roofObstacle,
            int roofChainIndex,
            float jumpTravel)
        {
            // Находит самый левый край chain до целевой крыши.
            float chainLeftEdge = roofObstacle.LeftX;

            for (int chainIndex = 0; chainIndex < roofChainIndex; chainIndex++)
            {
                ObstacleSnapshot obstacle = chain.Obstacles[chainIndex];

                if (obstacle.LeftX < chainLeftEdge)
                {
                    chainLeftEdge = obstacle.LeftX;
                }
            }

            // Ищет самый поздний безопасный старт: ещё не врезаться в chain и ещё не перелететь крышу.
            // Результат может быть отрицательным — это значит дедлайн прыжка уже в прошлом.
            // Такое окно отсекается в вызывающем методе проверкой lastFireShift > 0.
            float latestSafeFireShiftBeforeChainContact = chainLeftEdge - hamster.HamsterRightX;
            float latestSafeFireShiftBeforeRoofOvershoot = roofObstacle.RightX - jumpTravel - hamster.HamsterLeftX;
            float lastFireShift = Math.Min(
                latestSafeFireShiftBeforeChainContact,
                latestSafeFireShiftBeforeRoofOvershoot);

            return lastFireShift;
        }

        /// <summary>
        /// Пишет численную диагностику выбранного окна jump-on-roof.
        /// </summary>
        private void LogSelection(
            PlanningState planningState,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int roofChainIndex,
            float jumpTravel,
            float firstFireShift,
            float lastFireShift,
            float fireShift,
            bool hasExpectedOutcome,
            JumpResolveResult selectedOutcome)
        {
            ObstacleSnapshot triggerObstacle = chain.FirstObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();

            DebugManager.DiagLogVerbose(
                $"[{_policy.LogTag} WINDOW] target={targetObstacle.ObstacleType} " +
                $"targetIndex={targetObstacleIndex} roofChainIndex={roofChainIndex} " +
                $"travel={jumpTravel:F3} " +
                $"first={firstFireShift:F3} last={lastFireShift:F3} selected={fireShift:F3} " +
                $"boundaryMargin={fireWindowBoundaryMargin:F3} " +
                $"projectedTriggerX={projectedTriggerX:F3} " +
                $"renderWorldX={renderWorldX:F3} triggerLeft={triggerObstacle.LeftX:F3} " +
                $"targetLeft={targetObstacle.LeftX:F3} targetRight={targetObstacle.RightX:F3} " +
                $"hamsterLeft={planningState.Hamster.HamsterLeftX:F3} " +
                $"hamsterRight={planningState.Hamster.HamsterRightX:F3} " +
                $"projection={planningState.ProjectionWorldShift:F3} " +
                $"outcome={selectedOutcome.State} outcomeTargetIndex={selectedOutcome.TargetIndex} " +
                $"expectedOutcome={hasExpectedOutcome}");
        }

        /// <summary>
        /// Ищет самый ранний fire shift, который runtime resolver подтверждает как посадку на целевую крышу.
        /// </summary>
        private bool TryFindEarliestResolverValidFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float firstFireShift,
            float lastFireShift,
            float jumpTravel,
            int targetObstacleIndex,
            bool hasPreRoofObstacle,
            out float fireShift,
            out JumpResolveResult selectedOutcome)
        {
            // Проверяет левую границу окна.
            fireShift = firstFireShift;
            selectedOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);
            if (IsValidFireShift(targetObstacleIndex, selectedOutcome))
            {
                // При obstacle перед крышей не берём край окна: небольшой сдвиг позже снижает риск зацепить obstacle при посадке,
                // но точку всё равно подтверждает runtime resolver.
                if (hasPreRoofObstacle)
                {
                    float preferredFireShift =
                        fireShift
                        + (lastFireShift - fireShift) * _preRoofObstacleWindowOffsetRatio;
                    if (preferredFireShift > fireShift)
                    {
                        JumpResolveResult preferredOutcome = ResolveRuntimeOutcomeAtFireShift(
                            hamster,
                            baseObstacles,
                            preferredFireShift,
                            jumpTravel);
                        if (IsValidFireShift(targetObstacleIndex, preferredOutcome))
                        {
                            fireShift = preferredFireShift;
                            selectedOutcome = preferredOutcome;
                        }
                    }
                }

                return true;
            }

            // Проверяет правую границу окна.
            float rightFireShift = lastFireShift;
            JumpResolveResult rightOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                rightFireShift,
                jumpTravel);
            if (!IsValidFireShift(targetObstacleIndex, rightOutcome))
            {
                fireShift = rightFireShift;
                selectedOutcome = rightOutcome;
                return false;
            }

            // Сужает границу до первого валидного resolver outcome.
            float leftFireShift = firstFireShift;
            selectedOutcome = rightOutcome;
            for (int iteration = 0; iteration < _earliestFireShiftSearchIterations; iteration++)
            {
                float candidateFireShift = (leftFireShift + rightFireShift) * 0.5f;
                JumpResolveResult candidateOutcome = ResolveRuntimeOutcomeAtFireShift(
                    hamster,
                    baseObstacles,
                    candidateFireShift,
                    jumpTravel);

                if (IsValidFireShift(targetObstacleIndex, candidateOutcome))
                {
                    rightFireShift = candidateFireShift;
                    selectedOutcome = candidateOutcome;
                    continue;
                }

                leftFireShift = candidateFireShift;
            }

            // Возвращает найденную раннюю валидную точку.
            fireShift = rightFireShift;

            // При obstacle перед крышей не берём край окна: небольшой сдвиг позже снижает риск зацепить obstacle при посадке,
            // но точку всё равно подтверждает runtime resolver.
            if (hasPreRoofObstacle)
            {
                float preferredFireShift =
                    fireShift
                    + (lastFireShift - fireShift) * _preRoofObstacleWindowOffsetRatio;
                if (preferredFireShift > fireShift)
                {
                    JumpResolveResult preferredOutcome = ResolveRuntimeOutcomeAtFireShift(
                        hamster,
                        baseObstacles,
                        preferredFireShift,
                        jumpTravel);
                    if (IsValidFireShift(targetObstacleIndex, preferredOutcome))
                    {
                        fireShift = preferredFireShift;
                        selectedOutcome = preferredOutcome;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Проверяет outcome для выбранного fire shift.
        /// </summary>
        private bool IsValidFireShift(
            int targetObstacleIndex,
            JumpResolveResult outcome)
        {
            return IsExpectedOutcome(outcome, targetObstacleIndex);
        }

        /// <summary>
        /// Проверяет, что resolver outcome соответствует целевой крыше.
        /// </summary>
        private bool IsExpectedOutcome(
            JumpResolveResult outcome,
            int targetObstacleIndex)
        {
            return outcome.State == _policy.ExpectedRoofState
                   && outcome.TargetIndex == targetObstacleIndex;
        }

    }
}
