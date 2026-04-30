using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Подбирает момент срабатывания прыжка для посадки бота на целевую крышу.
    /// </summary>
    internal sealed class JumpOnRoofFireWindowFinder
    {
        private const float _windowEpsilon = 0.0001f;

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
            out float fireShift)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;
            fireShift = 0f;

            // Проверяет обязательные входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Находит валидную целевую крышу внутри chain.
            if (!TryGetRoofTarget(
                    planningState.Hamster,
                    chain,
                    out targetObstacle,
                    out targetObstacleIndex,
                    out int roofChainIndex))
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
                    out float firstFireShift,
                    out float lastFireShift,
                    out bool hasOverObstacles))
            {
                return false;
            }

                // Выбирает точку внутри математического окна с ранним direct-roof смещением.
                if (!TrySelectFireShift(
                    planningState.Hamster,
                    hasOverObstacles,
                    firstFireShift,
                    lastFireShift,
                    out fireShift,
                    out float directEarlyShift))
                {
                return false;
                }

            // Проверяет выбранный fire shift через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            bool hasExpectedOutcome = CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                jumpTravel,
                targetObstacleIndex);
            LogSelection(
                planningState,
                chain,
                targetObstacle,
                targetObstacleIndex,
                roofChainIndex,
                hasOverObstacles,
                jumpTravel,
                firstFireShift,
                lastFireShift,
                fireShift,
                directEarlyShift,
                hasExpectedOutcome);

            return hasExpectedOutcome;
        }

        /// <summary>
        /// Находит первую roof target внутри chain и отсекает занятую опасным occupant крышу.
        /// </summary>
        internal static bool TryGetRoofTarget(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int roofChainIndex)
        {
            targetObstacle = null;
            targetObstacleIndex = -1;
            roofChainIndex = -1;

            if (hamster == null || chain == null)
                return false;

            if (!chain.TryFindFirstRoof(out targetObstacle, out targetObstacleIndex, out roofChainIndex))
                return false;

            if (chain.HasDamagingRoofOccupant(roofChainIndex))
                return false;

            if (targetObstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

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
            out float lastFireShift,
            out bool hasOverObstacles)
        {
            // Сбрасывает результат и проверяет обязательные данные.
            firstFireShift = 0f;
            lastFireShift = 0f;
            hasOverObstacles = false;

            if (hamster == null
                || chain == null
                || roofObstacle == null)
            {
                return false;
            }

            // Собирает левый край chain и правый край промежуточных over obstacles.
            float chainLeftEdge = roofObstacle.LeftX;
            float overObstacleRightEdge = 0f;

            for (int chainIndex = 0; chainIndex < roofChainIndex; chainIndex++)
            {
                if (!chain.TryGetAt(chainIndex, out ObstacleSnapshot obstacle, out _))
                    return false;

                if (obstacle.IsBottomLine != roofObstacle.IsBottomLine)
                    return false;

                if (!ObstacleClassifier.CanJumpOverOnGround(obstacle.ObstacleType))
                    return false;

                if (!hasOverObstacles || obstacle.LeftX < chainLeftEdge)
                    chainLeftEdge = obstacle.LeftX;

                if (!hasOverObstacles || obstacle.RightX > overObstacleRightEdge)
                    overObstacleRightEdge = obstacle.RightX;

                hasOverObstacles = true;
            }

            // Открывает окно там, где прыжок уже достаёт до левого края крыши.
            float roofLeftEdgeShift = roofObstacle.LeftX - jumpTravel - hamster.HamsterRightX;
            firstFireShift = roofLeftEdgeShift;

            // При наличии over obstacles окно также должно перелетать их правый край.
            if (hasOverObstacles)
            {
                float overObstacleRightEdgeShift = overObstacleRightEdge - jumpTravel - hamster.HamsterLeftX;
                if (overObstacleRightEdgeShift > firstFireShift)
                    firstFireShift = overObstacleRightEdgeShift;
            }

            if (firstFireShift < 0f)
                firstFireShift = 0f;

            // Закрывает окно за один frame движения мира до ground-contact с левым краем chain.
            float chainLeftEdgeLimit = chainLeftEdge - hamster.HamsterRightX - GetExecutionSafetyShift();
            lastFireShift = chainLeftEdgeLimit;

            // Для chain-over посадки дополнительно не даёт перелететь правый край целевой крыши.
            if (hasOverObstacles)
            {
                float roofRightEdgeLimit = roofObstacle.RightX - jumpTravel - hamster.HamsterLeftX;
                lastFireShift = global::System.Math.Min(lastFireShift, roofRightEdgeLimit);
            }

            // Делает окно строго внутренним, как в jump-over chain calculators.
            firstFireShift += _windowEpsilon;
            lastFireShift -= _windowEpsilon;

            return lastFireShift > 0f && firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Выбирает точку уже рассчитанного fire-window для jump-on-roof.
        /// </summary>
        private static bool TrySelectFireShift(
            HamsterSnapshot hamster,
            bool hasOverObstacles,
            float firstFireShift,
            float lastFireShift,
            out float fireShift,
            out float directEarlyShift)
        {
            directEarlyShift = 0f;

            if (firstFireShift >= lastFireShift)
            {
                fireShift = 0f;
                return false;
            }

            // Для chain-over остаётся поздняя граница перед левым краем chain.
            fireShift = lastFireShift;

            // Для чистой крыши срабатывает на половину ширины хомяка раньше ради более плавной посадки.
            if (!hasOverObstacles && hamster != null)
            {
                directEarlyShift = hamster.Width / 2f;
                fireShift = global::System.Math.Max(firstFireShift, lastFireShift - directEarlyShift);
            }

            return firstFireShift < lastFireShift;
        }

        /// <summary>
        /// Возвращает запас в world-units на один кадр движения мира перед runtime collision.
        /// </summary>
        private static float GetExecutionSafetyShift()
        {
            float timeScale = global::UnityEngine.Time.timeScale;
            if (timeScale <= 0f)
                timeScale = 1f;

            return Consts.GameSpeedBase * timeScale / Consts.FPS;
        }

        /// <summary>
        /// Пишет численную диагностику выбранного окна jump-on-roof.
        /// </summary>
        private static void LogSelection(
            PlanningState planningState,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int roofChainIndex,
            bool hasOverObstacles,
            float jumpTravel,
            float firstFireShift,
            float lastFireShift,
            float fireShift,
            float directEarlyShift,
            bool hasExpectedOutcome)
        {
            ObstacleSnapshot triggerObstacle = chain.FirstObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;

            DebugManager.DiagLog(
                $"[JumpOnRoof WINDOW] target={targetObstacle.ObstacleType} " +
                $"targetIndex={targetObstacleIndex} roofChainIndex={roofChainIndex} " +
                $"hasOverObstacles={hasOverObstacles} jumpTravel={jumpTravel:F3} " +
                $"first={firstFireShift:F3} last={lastFireShift:F3} selected={fireShift:F3} " +
                $"directEarlyShift={directEarlyShift:F3} safety={GetExecutionSafetyShift():F3} " +
                $"projectedTriggerX={projectedTriggerX:F3} " +
                $"renderWorldX={renderWorldX:F3} triggerLeft={triggerObstacle.LeftX:F3} " +
                $"targetLeft={targetObstacle.LeftX:F3} targetRight={targetObstacle.RightX:F3} " +
                $"hamsterLeft={planningState.Hamster.HamsterLeftX:F3} " +
                $"hamsterRight={planningState.Hamster.HamsterRightX:F3} " +
                $"projection={planningState.ProjectionWorldShift:F3} " +
                $"expectedOutcome={hasExpectedOutcome}");
        }

        /// <summary>
        /// Проверяет, что fire shift приводит к runtime outcome JumpOnRoof по целевой крыше.
        /// </summary>
        internal static bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            int chainEndIndex)
        {
            // Строит obstacle snapshot на момент fire.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Готовит runtime context для обычного прыжка.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                jumpTravel,
                jumpTravel,
                damageBigAliveWithoutYByReach: true);

            // Сверяет runtime outcome с целевой крышей в конце chain.
            JumpResolveResult result = JumpOutcomeResolver.ResolveJump(obstaclesAtFireShift, context);
            return result.State == HamsterStateEnum.JumpOnRoof
                   && result.TargetIndex == chainEndIndex;
        }
    }
}