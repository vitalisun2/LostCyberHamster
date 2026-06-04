using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Строит role-based дерево решений для текущего planning-состояния.
    /// </summary>
    public sealed class PlanningGraphBuilderNew
    {
        private const int MaxSearchDepth = 6;

        private readonly ActionGeneratorNew _actionGenerator;
        private readonly TransitionSimulatorNew _transitionSimulator;
        private readonly DecisionPointDetectorNew _decisionPointDetector = new DecisionPointDetectorNew();

        /// <summary>
        /// Создает role-based построитель графа поверх нового генератора действий и нового симулятора.
        /// </summary>
        public PlanningGraphBuilderNew(ActionGeneratorNew actionGenerator, TransitionSimulatorNew transitionSimulator)
        {
            _actionGenerator = actionGenerator;
            _transitionSimulator = transitionSimulator;
        }

        /// <summary>
        /// Строит все достижимые role-based planning-ветки от корневого состояния.
        /// </summary>
        public IReadOnlyList<PlanningBranch> BuildBranches(WorldSnapshot worldSnapshot, PlanningState rootState)
        {
            if (worldSnapshot == null || rootState == null)
                return Array.Empty<PlanningBranch>();

            var branches = new List<PlanningBranch>();
            PlanningGraphNode rootNode = PlanningGraphNode.CreateRoot(rootState);
            var bestMetricsByState = new Dictionary<PlanningStateKey, PlanningBranchMetrics>
            {
                [rootNode.StateKey] = rootNode.Metrics
            };

            ExploreNode(rootNode, worldSnapshot, branches, bestMetricsByState);
            return branches;
        }

        /// <summary>
        /// Рекурсивно разворачивает role-based planning node в дочерние ветки.
        /// </summary>
        private void ExploreNode(
            PlanningGraphNode currentNode,
            WorldSnapshot worldSnapshot,
            List<PlanningBranch> branches,
            Dictionary<PlanningStateKey, PlanningBranchMetrics> bestMetricsByState)
        {
            // Останавливает поиск на заданной глубине.
            if (currentNode.Depth >= MaxSearchDepth)
            {
                if (!HasUnresolvedPlanningSituation(currentNode.State, worldSnapshot))
                    AddLeafBranch(currentNode, branches);

                return;
            }

            // Разворачивает все action-варианты из текущего projected-состояния.
            IReadOnlyList<PlannedAction> candidates = _actionGenerator.Generate(currentNode.State, worldSnapshot);
            if (candidates.Count == 0)
            {
                if (!HasUnresolvedPlanningSituation(currentNode.State, worldSnapshot))
                    AddLeafBranch(currentNode, branches);

                return;
            }

            // Строит дочерние узлы только через реальные действия.
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                PlannedAction candidate = candidates[candidateIndex];
                if (candidate == null)
                    continue;

                PlanningState nextState = _transitionSimulator.Simulate(currentNode.State, candidate, worldSnapshot);
                if (nextState == null || CreatesAncestorCycle(currentNode, nextState))
                    continue;

                PlanningGraphNode childNode = currentNode.CreateChild(nextState, candidate);
                if (IsDominated(childNode, bestMetricsByState))
                    continue;

                bestMetricsByState[childNode.StateKey] = childNode.Metrics;
                ExploreNode(childNode, worldSnapshot, branches, bestMetricsByState);
            }
        }

        /// <summary>
        /// Проверяет, осталась ли unresolved role-based planning-ситуация для projected-состояния.
        /// </summary>
        private bool HasUnresolvedPlanningSituation(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            // Проецирует snapshot перед проверкой новой точки решения.
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return false;

            return _decisionPointDetector.TryDetect(
                planningState,
                projectedWorldSnapshot,
                out _);
        }

        /// <summary>
        /// Добавляет leaf-ветку в итоговый список.
        /// </summary>
        private static void AddLeafBranch(PlanningGraphNode leafNode, List<PlanningBranch> branches)
        {
            if (leafNode == null)
                return;

            branches.Add(PlanningBranch.FromLeaf(leafNode));
        }

        /// <summary>
        /// Проверяет, доминирует ли уже найденная ветка над новым узлом с тем же state key.
        /// </summary>
        private static bool IsDominated(
            PlanningGraphNode candidateNode,
            Dictionary<PlanningStateKey, PlanningBranchMetrics> bestMetricsByState)
        {
            if (candidateNode == null || bestMetricsByState == null)
                return false;

            return bestMetricsByState.TryGetValue(candidateNode.StateKey, out PlanningBranchMetrics bestMetrics)
                && bestMetrics.IsCheaperOrEquivalentTo(candidateNode.Metrics);
        }

        /// <summary>
        /// Проверяет, не возвращает ли новый state ветку в уже посещенный ancestor state.
        /// </summary>
        private static bool CreatesAncestorCycle(PlanningGraphNode currentNode, PlanningState nextState)
        {
            PlanningStateKey nextKey = PlanningStateKey.FromState(nextState);
            for (PlanningGraphNode node = currentNode; node != null; node = node.Parent)
            {
                if (node.StateKey.Equals(nextKey))
                    return true;
            }

            return false;
        }
    }
}
