using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Выбирает target и roof context для role-based roof-to-road jump-on.
    /// </summary>
    internal sealed class JumpOnFromRoofActionResolver
    {
        /// <summary>
        /// Возвращает road action-chain с target и последнюю passive roof для расчёта action.
        /// </summary>
        public bool TryResolve(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleChain sourceChain,
            JumpOnFromRoofTravel travel,
            out ObstacleChain actionChain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex,
            out ObstacleSnapshot lastRoof,
            out string deadEndReason)
        {
            // Инициализирует пустой результат и проверяет вход.
            actionChain = null;
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            lastRoof = null;
            deadEndReason = null;
            if (planningState?.Hamster == null
                || worldSnapshot == null
                || sourceChain == null
                || sourceChain.Count <= 0
                || travel.RunFromRoofTravel <= 0f)
            {
                return false;
            }

            // Восстанавливает последнюю passive roof для roof-run ограничений.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out lastRoof,
                    out _))
            {
                return false;
            }

            // Использует исходную chain, если она уже содержит roof-to-road target.
            float maxTargetLeftX = GetMaxReachableTargetLeftX(
                planningState.Hamster,
                lastRoof,
                travel);
            if (TryResolveJumpOnFromRoofTarget(
                    planningState.Hamster,
                    sourceChain,
                    maxTargetLeftX,
                    out targetObstacle,
                    out targetObstacleIndex,
                    out targetObstacleChainIndex,
                    out deadEndReason))
            {
                actionChain = sourceChain;
            }
            else if (!string.IsNullOrEmpty(deadEndReason))
            {
                return false;
            }
            else if (!TryBuildRoadActionChain(
                         planningState,
                         worldSnapshot,
                         lastRoof,
                         maxTargetLeftX,
                         out actionChain,
                         out targetObstacle,
                         out targetObstacleIndex,
                         out targetObstacleChainIndex))
            {
                return false;
            }

            // Проверяет, есть ли смысл планировать roof jump-on в текущем ресурсе/опасности.
            return CanPlanJumpOnFromRoof(
                planningState.Hamster,
                actionChain.FirstObstacle,
                lastRoof,
                travel);
        }

        /// <summary>
        /// Находит первый target, фактически доступный для roof-to-road jump-on.
        /// </summary>
        private static bool TryResolveJumpOnFromRoofTarget(
            HamsterSnapshot hamster,
            ObstacleChain sourceChain,
            float maxTargetLeftX,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex,
            out string deadEndReason)
        {
            // Инициализирует пустой результат.
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            deadEndReason = null;
            if (hamster == null || sourceChain == null)
                return false;

            // Сканирует role-based chain до первого подходящего target.
            for (int chainIndex = 0; chainIndex < sourceChain.Count; chainIndex++)
            {
                ObstacleChainElement element = sourceChain.Elements[chainIndex];
                if (!IsJumpOnFromRoofTarget(element, hamster))
                    continue;

                if (element.Obstacle.LeftX > maxTargetLeftX)
                {
                    deadEndReason = "Нет безопасного окна для напрыгивания с крыши: target находится за правой границей безопасного окна запуска.";
                    return false;
                }

                targetObstacle = element.Obstacle;
                targetObstacleIndex = element.WorldIndex;
                targetObstacleChainIndex = chainIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Строит временную road action-chain после passive roof path до первого достижимого target.
        /// </summary>
        private static bool TryBuildRoadActionChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot lastRoof,
            float maxTargetLeftX,
            out ObstacleChain actionChain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex)
        {
            // Инициализирует пустой результат.
            actionChain = null;
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            var elements = new List<ObstacleChainElement>();

            // Сканирует road obstacles на текущей линии до первого target в пределах reach.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsRemovedInPlanning)
                    continue;

                if (obstacle.LeftX > maxTargetLeftX)
                    break;

                if (!TryCreateRoadActionElement(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        obstacleIndex,
                        lastRoof.RightX,
                        out ObstacleChainElement element))
                {
                    continue;
                }

                elements.Add(element);
                if (!IsJumpOnFromRoofTarget(element, planningState.Hamster))
                    continue;

                actionChain = ObstacleChain.FromOwnedElements(elements);
                targetObstacle = element.Obstacle;
                targetObstacleIndex = element.WorldIndex;
                targetObstacleChainIndex = elements.Count - 1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Создает element для road obstacle, который важен для roof-to-road jump-on проверки.
        /// </summary>
        private static bool TryCreateRoadActionElement(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            float roofRightEdgeX,
            out ObstacleChainElement element)
        {
            // Отсекает obstacle вне road-chain после passive roof path.
            element = null;
            HamsterSnapshot hamster = planningState.Hamster;
            if (obstacle == null
                || obstacle.IsRemovedInPlanning
                || obstacle.RightX <= hamster.HamsterLeftX
                || obstacle.LeftX < roofRightEdgeX
                || obstacle.IsBottomLine != hamster.IsOnBottomLine
                || ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
            {
                return false;
            }

            // Исключает roof occupant, который относится к текущему passive roof path.
            if (RoofRunProjection.TryFindPassiveRoofSupportForOccupant(
                    planningState,
                    worldSnapshot,
                    obstacle,
                    out _,
                    out _))
            {
                return false;
            }

            // Оставляет только blockers и roof-to-road targets.
            ObstacleRoleMask roleMask = ObstacleRoleClassifier.GetRoleMask(
                planningState,
                worldSnapshot,
                obstacle);
            var candidate = new ObstacleChainElement(obstacle, obstacleIndex, roleMask);
            if (!candidate.HasRole(ObstacleRole.BlockingThreat)
                && !IsJumpOnFromRoofTarget(candidate, hamster))
            {
                return false;
            }

            element = candidate;
            return true;
        }

        /// <summary>
        /// Возвращает true, если element является target для roof-to-road jump-on.
        /// </summary>
        private static bool IsJumpOnFromRoofTarget(
            ObstacleChainElement element,
            HamsterSnapshot hamster)
        {
            // Проверяет роль, линию и factual-тип obstacle.
            return element != null
                && hamster != null
                && !element.Obstacle.IsRemovedInPlanning
                && element.HasRole(ObstacleRole.Target)
                && element.Obstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.CanJumpOnFromRoofObstacle(element.Obstacle.ObstacleType);
        }

        /// <summary>
        /// Вычисляет правую границу target left edge, достижимую до конца passive RoofRun.
        /// </summary>
        private static float GetMaxReachableTargetLeftX(
            HamsterSnapshot hamster,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel)
        {
            // Учитывает последний возможный запуск до автоматического схода с крыши.
            float fireWindowMargin = JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            float latestRoofRunFireShift =
                lastRoof.RightX
                + Assets.Scripts.Consts.GetRoofRunPassiveContinuationGap(hamster.Width)
                - hamster.HamsterRightX
                - fireWindowMargin;

            return hamster.CenterX
                + travel.ResolveFireShiftOffset
                + travel.ResolveTravel
                + latestRoofRunFireShift
                - fireWindowMargin;
        }

        /// <summary>
        /// Проверяет, нужно ли планировать roof jump-on target при текущем ресурсе.
        /// </summary>
        private static bool CanPlanJumpOnFromRoof(
            HamsterSnapshot hamster,
            ObstacleSnapshot firstRoadObstacle,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel)
        {
            // Разрешает target-oriented action при достаточной энергии.
            if (JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster))
                return true;

            // Разрешает action как защиту, если passive exit приведет в road-chain.
            return IsDangerousAutomaticRoofExit(firstRoadObstacle, lastRoof, travel);
        }

        /// <summary>
        /// Возвращает true, если простой автоматический сход с крыши попадёт в ближайшую road-chain.
        /// </summary>
        private static bool IsDangerousAutomaticRoofExit(
            ObstacleSnapshot firstRoadObstacle,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel)
        {
            // Проверяет наличие геометрии для сравнения.
            if (firstRoadObstacle == null || lastRoof == null)
                return false;

            // Сравнивает gap с дистанцией автоматического схода.
            float gapToFirstRoadObstacle = firstRoadObstacle.LeftX - lastRoof.RightX;
            return gapToFirstRoadObstacle < travel.RunFromRoofTravel;
        }
    }
}
