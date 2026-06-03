using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Хранит one-line цепочку role-based obstacle elements для одной planning-ситуации.
    /// </summary>
    public sealed class ObstacleChainNew
    {
        /// <summary>
        /// Создает role-based chain для одной focus lane.
        /// </summary>
        public ObstacleChainNew(
            IReadOnlyList<ObstacleChainElementNew> elements,
            bool focusBottomLine)
        {
            if (elements == null)
                throw new ArgumentNullException(nameof(elements));

            if (elements.Count == 0)
                throw new ArgumentException("Obstacle chain must contain at least one element.", nameof(elements));

            var copiedElements = new List<ObstacleChainElementNew>(elements.Count);
            float leftX = float.MaxValue;
            float rightX = float.MinValue;

            for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
            {
                ObstacleChainElementNew element = elements[elementIndex]
                    ?? throw new ArgumentException("Obstacle chain cannot contain null elements.", nameof(elements));

                if (element.IsBottomLine != focusBottomLine)
                    throw new ArgumentException("Obstacle chain must contain elements from one focus lane.", nameof(elements));

                copiedElements.Add(element);

                if (element.Obstacle.LeftX < leftX)
                    leftX = element.Obstacle.LeftX;

                if (element.Obstacle.RightX > rightX)
                    rightX = element.Obstacle.RightX;
            }

            Elements = copiedElements;
            FocusBottomLine = focusBottomLine;
            LeftX = leftX;
            RightX = rightX;
        }

        public IReadOnlyList<ObstacleChainElementNew> Elements { get; }
        public bool FocusBottomLine { get; }
        public int Count => Elements.Count;
        public ObstacleChainElementNew First => Elements[0];
        public ObstacleSnapshot FirstObstacle => First.Obstacle;
        public int FirstIndex => First.WorldIndex;
        public float LeftX { get; }
        public float RightX { get; }

        /// <summary>
        /// Возвращает element по индексу внутри chain.
        /// </summary>
        public bool TryGetAt(
            int chainIndex,
            out ObstacleChainElementNew element)
        {
            if (chainIndex < 0 || chainIndex >= Count)
            {
                element = null;
                return false;
            }

            element = Elements[chainIndex];
            return true;
        }

        /// <summary>
        /// Находит первый element с указанной role.
        /// </summary>
        public bool TryFindFirstWithRole(
            ObstacleRole role,
            out ObstacleChainElementNew element,
            out int chainIndex)
        {
            for (int index = 0; index < Count; index++)
            {
                ObstacleChainElementNew candidate = Elements[index];
                if (!candidate.HasRole(role))
                    continue;

                element = candidate;
                chainIndex = index;
                return true;
            }

            element = null;
            chainIndex = -1;
            return false;
        }

        /// <summary>
        /// Проверяет, входит ли obstacle с указанным instance id в chain.
        /// </summary>
        public bool ContainsObstacle(ObstacleSnapshot targetObstacle)
        {
            if (targetObstacle == null)
                return false;

            for (int chainIndex = 0; chainIndex < Count; chainIndex++)
            {
                if (Elements[chainIndex].Obstacle.InstanceId == targetObstacle.InstanceId)
                    return true;
            }

            return false;
        }
    }
}
