using System.Collections.Generic;
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
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Выбирает точку внутри уже суженного fire-window.
            if (!TrySelectFireShift(
                    firstFireShift,
                    lastFireShift,
                    roofChainIndex > 0,
                    out fireShift))
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
                jumpTravel,
                firstFireShift,
                lastFireShift,
                fireShift,
                hasExpectedOutcome);

            return hasExpectedOutcome;
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

            // Проверяет наличие smallNotAliveRoadAndRoof на крыше, который может помешать посадке.
            if (chain.HasDamagingRoofOccupant(roofChainIndex))
                return false;

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
            firstFireShift += JumpPlanningConstants.FireWindowBoundaryMargin;
            lastFireShift -= JumpPlanningConstants.FireWindowBoundaryMargin;

            return lastFireShift > 0f && firstFireShift < lastFireShift;
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
            float firstFireShift = roofObstacle.LeftX - jumpTravel - hamster.HamsterRightX;

            // Ограничивает окно нулевым минимальным сдвигом.
            if (firstFireShift < 0f)
            {
                firstFireShift = 0f;
            }

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
            float latestSafeFireShiftBeforeChainContact = chainLeftEdge - hamster.HamsterRightX;
            float latestSafeFireShiftBeforeRoofOvershoot = roofObstacle.RightX - jumpTravel - hamster.HamsterLeftX;
            return global::System.Math.Min(
                latestSafeFireShiftBeforeChainContact,
                latestSafeFireShiftBeforeRoofOvershoot);
        }

        /// <summary>
        /// Выбирает точку уже рассчитанного fire-window для jump-on-roof.
        /// </summary>
        private static bool TrySelectFireShift(
            float firstFireShift,
            float lastFireShift,
            bool hasPreRoofObstacle,
            out float fireShift)
        {
            if (firstFireShift >= lastFireShift)
            {
                fireShift = 0f;
                return false;
            }

            if (hasPreRoofObstacle)
            {
                fireShift = lastFireShift;
                return fireShift > firstFireShift;
            }

            // Для прямой крыши без препятствий перед ней берёт центр безопасного окна.
            fireShift = (firstFireShift + lastFireShift) * 0.5f;
            return true;
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
            float jumpTravel,
            float firstFireShift,
            float lastFireShift,
            float fireShift,
            bool hasExpectedOutcome)
        {
            ObstacleSnapshot triggerObstacle = chain.FirstObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float renderWorldX = projectedTriggerX + planningState.ProjectionWorldShift;

            DebugManager.DiagLog(
                $"[JumpOnRoof WINDOW] target={targetObstacle.ObstacleType} " +
                $"targetIndex={targetObstacleIndex} roofChainIndex={roofChainIndex} " +
                $"jumpTravel={jumpTravel:F3} " +
                $"first={firstFireShift:F3} last={lastFireShift:F3} selected={fireShift:F3} " +
                $"boundaryMargin={JumpPlanningConstants.FireWindowBoundaryMargin:F3} " +
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
            if (!JumpFireSafety.CanWaitUntilFire(hamster, baseObstacles, fireShift))
                return false;

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
