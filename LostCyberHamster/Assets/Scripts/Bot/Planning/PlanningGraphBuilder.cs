using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Строит дерево решений для текущего planning-состояния.
    /// </summary>
    public sealed class PlanningGraphBuilder
    {
        private const int MaxSearchDepth = 6;

        private readonly ActionGenerator _actionGenerator;
        private readonly TransitionSimulator _transitionSimulator;
        private readonly DecisionPointDetector _decisionPointDetector = new DecisionPointDetector();

        /// <summary>
        /// Создает построитель графа поверх генератора действий и симулятора.
        /// </summary>
        public PlanningGraphBuilder(ActionGenerator actionGenerator, TransitionSimulator transitionSimulator)
        {
            _actionGenerator = actionGenerator;
            _transitionSimulator = transitionSimulator;
        }

        /// <summary>
        /// Строит все достижимые planning-ветки от корневого состояния.
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

        private void ExploreNode(
            PlanningGraphNode currentNode,
            WorldSnapshot worldSnapshot,
            List<PlanningBranch> branches,
            Dictionary<PlanningStateKey, PlanningBranchMetrics> bestMetricsByState)
        {
            // Stop expanding when the search reached the configured horizon.
            if (currentNode.Depth >= MaxSearchDepth)
            {
                if (!HasUnresolvedBlockingDecision(currentNode.State, worldSnapshot))
                    AddLeafBranch(currentNode, branches);

                return;
            }

            // Expand all action variants available from the current projected state.
            IReadOnlyList<PlannedAction> candidates = _actionGenerator.Generate(currentNode.State, worldSnapshot);
            if (candidates.Count == 0)
            {
                if (!HasUnresolvedBlockingDecision(currentNode.State, worldSnapshot))
                    AddLeafBranch(currentNode, branches);

                return;
            }

            bool expandedAnyChild = false;
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
                expandedAnyChild = true;
            }

            // Keep the current chain as a leaf when no candidate produced a valid new state.
            if (!expandedAnyChild && !HasUnresolvedBlockingDecision(currentNode.State, worldSnapshot))
                AddLeafBranch(currentNode, branches);
        }

        private bool HasUnresolvedBlockingDecision(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return false;

            return _decisionPointDetector.TryDetect(planningState, projectedWorldSnapshot, out _);
        }

        private static void AddLeafBranch(PlanningGraphNode leafNode, List<PlanningBranch> branches)
        {
            if (leafNode == null || leafNode.IsRoot)
                return;

            branches.Add(PlanningBranch.FromLeaf(leafNode));
        }

        private static bool IsDominated(
            PlanningGraphNode candidateNode,
            Dictionary<PlanningStateKey, PlanningBranchMetrics> bestMetricsByState)
        {
            if (candidateNode == null || bestMetricsByState == null)
                return false;

            return bestMetricsByState.TryGetValue(candidateNode.StateKey, out PlanningBranchMetrics bestMetrics)
                && bestMetrics.IsCheaperOrEquivalentTo(candidateNode.Metrics);
        }

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
