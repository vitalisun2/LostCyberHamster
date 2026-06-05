using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Строит one-line role-based chain для текущей линии хомяка.
    /// </summary>
    internal sealed class ObstacleChainBuilderNew
    {
        /// <summary>
        /// Пытается построить chain от ближайшего active obstacle на текущей линии.
        /// </summary>
        public bool TryBuild(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            out ObstacleChainNew chain)
        {
            chain = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            bool currentBottomLine = planningState.IsOnBottomLine;
            if (!TryFindFirstActiveElement(
                    planningState,
                    worldSnapshot,
                    currentBottomLine,
                    firstObstacleIndex,
                    out ObstacleChainElementNew firstElement))
            {
                return false;
            }

            List<ObstacleChainElementNew> elements = BuildChainElements(
                planningState,
                worldSnapshot,
                currentBottomLine,
                firstElement);

            chain = new ObstacleChainNew(elements);
            return true;
        }

        /// <summary>
        /// Находит ближайший active obstacle на текущей линии.
        /// </summary>
        private static bool TryFindFirstActiveElement(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool currentBottomLine,
            int firstObstacleIndex,
            out ObstacleChainElementNew element)
        {
            element = null;
            int startIndex = firstObstacleIndex < 0 ? 0 : firstObstacleIndex;

            for (int obstacleIndex = startIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!TryCreateActiveElement(
                        planningState,
                        worldSnapshot,
                        currentBottomLine,
                        obstacle,
                        obstacleIndex,
                        out ObstacleChainElementNew candidate))
                {
                    continue;
                }

                element = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Расширяет chain близкими active obstacles той же текущей линии.
        /// </summary>
        private static List<ObstacleChainElementNew> BuildChainElements(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool currentBottomLine,
            ObstacleChainElementNew firstElement)
        {
            var elements = new List<ObstacleChainElementNew> { firstElement };
            float previousRightX = firstElement.Obstacle.RightX;

            for (int obstacleIndex = firstElement.WorldIndex + 1; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!TryCreateActiveElement(
                        planningState,
                        worldSnapshot,
                        currentBottomLine,
                        obstacle,
                        obstacleIndex,
                        out ObstacleChainElementNew element))
                {
                    continue;
                }

                float gap = element.Obstacle.LeftX - previousRightX;
                if (gap >= planningState.Hamster.Width)
                    break;

                elements.Add(element);

                if (element.Obstacle.RightX > previousRightX)
                    previousRightX = element.Obstacle.RightX;
            }

            return elements;
        }

        /// <summary>
        /// Создает active element, если obstacle относится к текущей линии и участвует в planning.
        /// </summary>
        private static bool TryCreateActiveElement(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool currentBottomLine,
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            out ObstacleChainElementNew element)
        {
            element = null;

            if (obstacle == null)
                return false;

            if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                return false;

            if (obstacle.IsBottomLine != currentBottomLine)
                return false;

            HashSet<ObstacleRole> roles = ObstacleRoleClassifierNew.GetRoles(
                planningState,
                worldSnapshot,
                obstacle);

            element = new ObstacleChainElementNew(obstacle, obstacleIndex, roles);
            if (!element.HasAnyActivePlanningRole)
                return false;

            return true;
        }
    }
}
