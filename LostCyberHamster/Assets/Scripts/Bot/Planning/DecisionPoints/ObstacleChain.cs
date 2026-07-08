using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Diagnostics;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Хранит one-line цепочку role-based obstacle elements для одной planning-ситуации.
    /// </summary>
    public sealed class ObstacleChain
    {
        internal static ObstacleChain FromOwnedElements(List<ObstacleChainElement> elements)
        {
            return new ObstacleChain(elements, copyElements: false);
        }

        /// <summary>
        /// Создает role-based chain для одной линии.
        /// </summary>
        public ObstacleChain(IReadOnlyList<ObstacleChainElement> elements)
            : this(elements, copyElements: true)
        {
        }

        private ObstacleChain(
            IReadOnlyList<ObstacleChainElement> elements,
            bool copyElements)
        {
            if (elements == null)
                throw new ArgumentNullException(nameof(elements));

            if (elements.Count == 0)
                throw new ArgumentException("Obstacle chain must contain at least one element.", nameof(elements));

            List<ObstacleChainElement> copiedElements = copyElements
                ? new List<ObstacleChainElement>(elements.Count)
                : null;
            float leftX = float.MaxValue;
            float rightX = float.MinValue;
            bool chainBottomLine = elements[0]?.IsBottomLine
                ?? throw new ArgumentException("Obstacle chain cannot contain null elements.", nameof(elements));

            for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
            {
                ObstacleChainElement element = elements[elementIndex]
                    ?? throw new ArgumentException("Obstacle chain cannot contain null elements.", nameof(elements));

                if (element.IsBottomLine != chainBottomLine)
                    throw new ArgumentException("Obstacle chain must contain elements from one focus lane.", nameof(elements));

                if (copyElements)
                    copiedElements.Add(element);

                if (element.Obstacle.LeftX < leftX)
                    leftX = element.Obstacle.LeftX;

                if (element.Obstacle.RightX > rightX)
                    rightX = element.Obstacle.RightX;
            }

            Elements = copyElements
                ? copiedElements
                : elements;
            LeftX = leftX;
            RightX = rightX;
            RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.ObstacleChainConstructed);
            if (copyElements)
            {
                RuntimePerformanceDiagnostics.Count(
                    RuntimePerformanceCounter.ObstacleChainCopiedElements,
                    copiedElements.Count);
            }
        }

        public IReadOnlyList<ObstacleChainElement> Elements { get; }
        public int Count => Elements.Count;
        public ObstacleChainElement First => Elements[0];
        public ObstacleSnapshot FirstObstacle => First.Obstacle;
        public int FirstIndex => First.WorldIndex;
        public float LeftX { get; }
        public float RightX { get; }

        /// <summary>
        /// Возвращает element по индексу внутри chain.
        /// </summary>
        public bool TryGetAt(
            int chainIndex,
            out ObstacleChainElement element)
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
            out ObstacleChainElement element,
            out int chainIndex)
        {
            for (int index = 0; index < Count; index++)
            {
                ObstacleChainElement candidate = Elements[index];
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

        /// <summary>
        /// Возвращает true, если в chain есть obstacle, который требует обязательного planning-решения.
        /// </summary>
        public bool HasAnyRequiredPlanningRole()
        {
            for (int chainIndex = 0; chainIndex < Count; chainIndex++)
            {
                if (Elements[chainIndex].HasAnyRequiredPlanningRole)
                    return true;
            }

            return false;
        }
    }
}
