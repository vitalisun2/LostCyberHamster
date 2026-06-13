using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Строит action-chain для ground jump-on, не расширяя сам role-based decision point.
    /// </summary>
    internal sealed class JumpOnActionChainResolver
    {
        /// <summary>
        /// Возвращает chain до первого достижимого ground target на линии исходной ситуации.
        /// </summary>
        public bool TryResolve(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleChain sourceChain,
            JumpOnTravel travel,
            out ObstacleChain actionChain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex,
            out string deadEndReason)
        {
            // Проверяет входные данные.
            actionChain = null;
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            deadEndReason = null;
            if (planningState?.Hamster == null
                || worldSnapshot?.Obstacles == null
                || sourceChain == null
                || sourceChain.Count <= 0
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            // Использует исходную chain, если target уже входит в текущую ситуацию.
            float maxTargetLeftX = GetMaxReachableTargetLeftX(
                planningState.Hamster,
                sourceChain.LeftX,
                travel);
            if (TryFindGroundTarget(
                    sourceChain,
                    out targetObstacle,
                    out targetObstacleIndex,
                    out targetObstacleChainIndex))
            {
                if (targetObstacle.LeftX > maxTargetLeftX)
                {
                    deadEndReason = "Нет безопасного окна для напрыгивания: target находится за правой границей безопасного окна запуска.";
                    return false;
                }

                actionChain = sourceChain;
                return true;
            }

            // Расширяет только область проверки action до первого достижимого target.
            return TryBuildExtendedChain(
                planningState,
                worldSnapshot,
                sourceChain,
                maxTargetLeftX,
                out actionChain,
                out targetObstacle,
                out targetObstacleIndex,
                out targetObstacleChainIndex);
        }

        /// <summary>
        /// Ищет первый ground target внутри уже собранной chain.
        /// </summary>
        private static bool TryFindGroundTarget(
            ObstacleChain chain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex)
        {
            // Сбрасывает результат и проверяет chain.
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            if (chain == null)
                return false;

            // Ищет первый target, который подходит для ground jump-on.
            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElement element = chain.Elements[chainIndex];
                if (!IsGroundTarget(element))
                    continue;

                targetObstacle = element.Obstacle;
                targetObstacleIndex = element.WorldIndex;
                targetObstacleChainIndex = chainIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Строит временную action-chain до первого target, игнорируя gap только внутри jump-on проверки.
        /// </summary>
        private static bool TryBuildExtendedChain(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleChain sourceChain,
            float maxTargetLeftX,
            out ObstacleChain actionChain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex)
        {
            // Подготавливает исходные элементы.
            actionChain = null;
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            var elements = new List<ObstacleChainElement>(sourceChain.Elements);
            bool chainBottomLine = sourceChain.First.IsBottomLine;
            int scanStartIndex = sourceChain.Elements[sourceChain.Count - 1].WorldIndex + 1;

            // Сканирует same-lane threats до первого target в пределах reach текущего jump-on policy.
            for (int obstacleIndex = scanStartIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.LeftX > maxTargetLeftX)
                    break;

                if (!TryCreateActionElement(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        obstacleIndex,
                        chainBottomLine,
                        out ObstacleChainElement element))
                {
                    continue;
                }

                elements.Add(element);
                if (!IsGroundTarget(element))
                    continue;

                actionChain = new ObstacleChain(elements);
                targetObstacle = element.Obstacle;
                targetObstacleIndex = element.WorldIndex;
                targetObstacleChainIndex = elements.Count - 1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Создает element для obstacle, который важен для проверки ground jump-on.
        /// </summary>
        private static bool TryCreateActionElement(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            bool chainBottomLine,
            out ObstacleChainElement element)
        {
            // Отсекает obstacle вне линии или позади хомяка.
            element = null;
            if (obstacle == null
                || obstacle.IsRemovedInPlanning
                || obstacle.RightX <= planningState.Hamster.HamsterLeftX
                || obstacle.IsBottomLine != chainBottomLine)
            {
                return false;
            }

            // Оставляет только blockers и ground targets, нужные для jump-on window.
            HashSet<ObstacleRole> roles = ObstacleRoleClassifier.GetRoles(
                planningState,
                worldSnapshot,
                obstacle);
            var candidate = new ObstacleChainElement(obstacle, obstacleIndex, roles);
            if (!candidate.HasRole(ObstacleRole.BlockingThreat) && !IsGroundTarget(candidate))
                return false;

            element = candidate;
            return true;
        }

        /// <summary>
        /// Возвращает true для role-based target, на который можно напрыгнуть с дороги.
        /// </summary>
        private static bool IsGroundTarget(ObstacleChainElement element)
        {
            return element != null
                && element.HasRole(ObstacleRole.Target)
                && ObstacleClassifier.CanJumpOnGroundObstacle(element.Obstacle.ObstacleType);
        }

        /// <summary>
        /// Вычисляет правую границу target left edge, при которой ещё возможно открыть fire-window.
        /// </summary>
        private static float GetMaxReachableTargetLeftX(
            HamsterSnapshot hamster,
            float chainLeftX,
            JumpOnTravel travel)
        {
            float fireWindowMargin = JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            float hamsterHalfWidth = hamster.Width * 0.5f;
            return chainLeftX
                + travel.ResolveFireShiftOffset
                + travel.ResolveTravel
                - hamsterHalfWidth
                - fireWindowMargin * 2f;
        }
    }
}
