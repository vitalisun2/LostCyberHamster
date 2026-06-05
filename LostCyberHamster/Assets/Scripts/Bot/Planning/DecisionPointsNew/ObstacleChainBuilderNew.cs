using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Builds a one-line role-based chain for the selected focus lane.
    /// </summary>
    internal sealed class ObstacleChainBuilderNew
    {
        /// <summary>
        /// Tries to build a chain from the nearest active obstacle on the current lane.
        /// </summary>
        public bool TryBuild(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            out ObstacleChainNew chain)
        {
            chain = null;
            if (planningState?.Hamster == null)
                return false;

            return TryBuild(
                planningState,
                worldSnapshot,
                firstObstacleIndex,
                planningState.IsOnBottomLine,
                out chain);
        }

        /// <summary>
        /// Tries to build a chain from the nearest active obstacle on the selected focus lane.
        /// </summary>
        public bool TryBuild(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            int firstObstacleIndex,
            bool focusBottomLine,
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

            chain = new ObstacleChainNew(elements);
            return true;
        }

        /// <summary>
        /// Finds the nearest active obstacle on the focus lane.
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
        /// Extends the chain with nearby active obstacles on the same focus lane.
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
        /// Creates an active element when the obstacle belongs to the focus lane and participates in planning.
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
