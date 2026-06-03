using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Строит one-line role-based chain для выбранной focus lane.
    /// </summary>
    internal sealed class ObstacleChainBuilderNew
    {
        /// <summary>
        /// Пытается построить chain от ближайшего active obstacle на focus lane.
        /// </summary>
        public bool TryBuild(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            int firstObstacleIndex,
            out ObstacleChainNew chain)
        {
            chain = null;
            if (planningState?.Hamster == null || worldSnapshot?.Obstacles == null)
                return false;

            if (!TryFindFirstActiveElement(
                    planningState,
                    worldSnapshot,
                    focusBottomLine,
                    firstObstacleIndex,
                    out ObstacleChainElementNew firstElement))
            {
                return false;
            }

            List<ObstacleChainElementNew> elements = BuildChainElements(
                planningState,
                worldSnapshot,
                focusBottomLine,
                firstElement);

            chain = new ObstacleChainNew(elements, focusBottomLine);
            return true;
        }

        /// <summary>
        /// Находит ближайший active obstacle на focus lane.
        /// </summary>
        private static bool TryFindFirstActiveElement(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
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
                        focusBottomLine,
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
        /// Расширяет chain близкими active obstacles той же focus lane.
        /// </summary>
        private static List<ObstacleChainElementNew> BuildChainElements(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
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
                        focusBottomLine,
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
        /// Создает active element, если obstacle относится к focus lane и участвует в planning.
        /// </summary>
        private static bool TryCreateActiveElement(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            bool focusBottomLine,
            ObstacleSnapshot obstacle,
            int obstacleIndex,
            out ObstacleChainElementNew element)
        {
            element = null;

            if (obstacle == null)
                return false;

            if (obstacle.RightX <= planningState.Hamster.HamsterLeftX)
                return false;

            if (obstacle.IsBottomLine != focusBottomLine)
                return false;

            ObstacleRole roles = ObstacleRoleClassifierNew.GetRoles(
                planningState,
                worldSnapshot,
                obstacle);

            element = new ObstacleChainElementNew(obstacle, obstacleIndex, roles);
            if (!element.HasAnyActivePlanningRole)
                return false;

            if (IsPassiveRoofOnlyContinuation(planningState, worldSnapshot, obstacle, roles))
                return false;

            return true;
        }

        /// <summary>
        /// Отсекает passive roof continuation, если там нет отдельной roof occupant hazard.
        /// </summary>
        private static bool IsPassiveRoofOnlyContinuation(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            ObstacleRole roles)
        {
            if ((roles & ObstacleRole.RoofOccupantHazard) != ObstacleRole.None)
                return false;

            return RoofRunProjection.IsPassiveRoofContinuation(
                planningState,
                worldSnapshot,
                obstacle);
        }
    }
}
