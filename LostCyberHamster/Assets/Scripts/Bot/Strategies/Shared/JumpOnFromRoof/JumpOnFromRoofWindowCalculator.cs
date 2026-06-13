using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Вычисляет fire-window для role-based roof-to-road jump-on target.
    /// </summary>
    internal static class JumpOnFromRoofWindowCalculator
    {
        /// <summary>
        /// Допуск к правому краю target относительно ширины хомяка.
        /// </summary>
        private const float RightEdgeToleranceRatio = 0.2f;

        /// <summary>
        /// Расширение bigAlive target-интервала относительно ширины obstacle.
        /// </summary>
        private const float BigAliveTargetExpansionRatio = 0.3f;

        /// <summary>
        /// Вычисляет fire-window для выбранного roof-to-road target внутри role-based chain.
        /// </summary>
        public static bool TryCalculate(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel,
            out JumpOnFromRoofWindowModel window,
            out string deadEndReason)
        {
            // Проверяет входы.
            window = default;
            deadEndReason = null;
            if (hamster == null
                || chain == null
                || targetObstacle == null
                || lastRoof == null
                || chain.Count <= 0
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            // Вычисляет открытые границы окна.
            if (!TryGetOpenWindow(
                    hamster,
                    chain,
                    targetObstacle,
                    targetObstacleChainIndex,
                    lastRoof,
                    travel,
                    out float firstFireShift,
                    out float lastFireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Собирает модель окна.
            float selectedFireShift = (firstFireShift + lastFireShift) * 0.5f;
            window = new JumpOnFromRoofWindowModel(
                targetObstacle,
                targetObstacleIndex,
                targetObstacleChainIndex,
                lastRoof,
                firstFireShift,
                lastFireShift,
                selectedFireShift);
            return true;
        }

        /// <summary>
        /// Вычисляет открытые границы запуска между target-hit, roof-run лимитом и pre-target clearance.
        /// </summary>
        private static bool TryGetOpenWindow(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleChainIndex,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel,
            out float firstFireShift,
            out float lastFireShift,
            out string deadEndReason)
        {
            // Вычисляет target-интервал runtime resolver-а.
            deadEndReason = null;
            GetTargetInterval(
                hamster,
                targetObstacle,
                out float targetLeftX,
                out float targetRightX);

            // Строит базовое окно по попаданию центра хомяка внутрь target.
            firstFireShift =
                targetLeftX
                - travel.ResolveFireShiftOffset
                - travel.ResolveTravel
                - hamster.CenterX;
            lastFireShift =
                targetRightX
                - travel.ResolveFireShiftOffset
                - travel.ResolveTravel
                - hamster.CenterX;
            float lastFireShiftBeforeRoofRunLimit = lastFireShift;

            // Применяет ограничения до target и до окончания RoofRun.
            float firstFireShiftBeforePreTargetClearance = firstFireShift;
            ApplyPreTargetClearanceLimit(
                hamster,
                chain,
                targetObstacleChainIndex,
                travel,
                ref firstFireShift);
            bool preTargetClearanceMovedWindow = firstFireShift > firstFireShiftBeforePreTargetClearance;
            ApplyRoofRunLimit(
                hamster,
                lastRoof,
                ref lastFireShift);
            bool roofRunLimitMovedWindow = lastFireShift < lastFireShiftBeforeRoofRunLimit;

            if (firstFireShift < 0f)
                firstFireShift = 0f;

            if (lastFireShift <= 0f || firstFireShift >= lastFireShift)
            {
                deadEndReason = BuildRawWindowDeadEndReason(
                    preTargetClearanceMovedWindow,
                    roofRunLimitMovedWindow);
                return false;
            }

            // Сужает окно на общий safety margin.
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;
            if (firstFireShift >= lastFireShift)
            {
                deadEndReason = "Safety margin не оставил безопасного окна для напрыгивания с крыши.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Выбирает короткую причину схлопывания raw-окна roof-to-road jump-on.
        /// </summary>
        private static string BuildRawWindowDeadEndReason(
            bool preTargetClearanceMovedWindow,
            bool roofRunLimitMovedWindow)
        {
            if (preTargetClearanceMovedWindow)
                return "Нет безопасного окна для напрыгивания с крыши: препятствие перед target закрывает траекторию.";

            if (roofRunLimitMovedWindow)
                return "Нет безопасного окна для напрыгивания с крыши: окно попадания в target не пересекается с окном движения по текущей крыше.";

            return "Нет безопасного окна для напрыгивания с крыши: target не пересекается с допустимой траекторией прыжка.";
        }

        /// <summary>
        /// Возвращает target-интервал, в который должен попасть центр хомяка в resolver-точке.
        /// </summary>
        private static void GetTargetInterval(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            out float targetLeftX,
            out float targetRightX)
        {
            // Берёт базовые границы obstacle.
            targetLeftX = targetObstacle.LeftX;
            targetRightX = targetObstacle.RightX;

            // Расширяет bigAlive так же, как runtime roof-jump resolver.
            if (targetObstacle.ObstacleType == ObstacleTypeEnum.bigAlive)
            {
                float expansion = (targetObstacle.RightX - targetObstacle.LeftX) * BigAliveTargetExpansionRatio;
                targetLeftX -= expansion;
                targetRightX += expansion;
            }

            // Добавляет правый допуск хомяка.
            targetRightX += hamster.Width * RightEdgeToleranceRatio;
        }

        /// <summary>
        /// Сдвигает левую границу до момента, когда pre-target obstacles не блокируют resolver.
        /// </summary>
        private static void ApplyPreTargetClearanceLimit(
            HamsterSnapshot hamster,
            ObstacleChain chain,
            int targetObstacleChainIndex,
            JumpOnFromRoofTravel travel,
            ref float firstFireShift)
        {
            // Проверяет препятствия до target.
            for (int chainIndex = 0; chainIndex < targetObstacleChainIndex; chainIndex++)
            {
                ObstacleSnapshot obstacle = chain.Elements[chainIndex].Obstacle;
                float earliestAfterObstacle =
                    obstacle.RightX
                    - travel.ResolveFireShiftOffset
                    - travel.ResolveTravel
                    - hamster.HamsterLeftX;

                if (earliestAfterObstacle > firstFireShift)
                    firstFireShift = earliestAfterObstacle;
            }
        }

        /// <summary>
        /// Уменьшает правую границу до последнего запуска перед окончанием passive RoofRun.
        /// </summary>
        private static void ApplyRoofRunLimit(
            HamsterSnapshot hamster,
            ObstacleSnapshot lastRoof,
            ref float lastFireShift)
        {
            // Вычисляет момент, после которого runtime перейдет в автоматический сход с крыши.
            float latestRoofRunFireShift =
                lastRoof.RightX
                + hamster.Width * RoofRunProjection.PassiveContinuationGapFactor
                - hamster.HamsterRightX;

            lastFireShift = Math.Min(lastFireShift, latestRoofRunFireShift);
        }
    }
}
