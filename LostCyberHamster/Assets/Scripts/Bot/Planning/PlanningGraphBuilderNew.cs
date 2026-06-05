using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Builds a role-based decision tree for the current planning state.
    /// </summary>
    public sealed class PlanningGraphBuilderNew
    {
        private const int MaxSearchDepth = 6;

        private readonly ActionGeneratorNew _actionGenerator;
        private readonly TransitionSimulatorNew _transitionSimulator;
        private readonly DecisionPointDetectorNew _decisionPointDetector = new DecisionPointDetectorNew();

        /// <summary>
        /// Creates a role-based graph builder over the new action generator and transition simulator.
        /// </summary>
        public PlanningGraphBuilderNew(ActionGeneratorNew actionGenerator, TransitionSimulatorNew transitionSimulator)
        {
            _actionGenerator = actionGenerator;
            _transitionSimulator = transitionSimulator;
        }

        /// <summary>
        /// Builds all reachable role-based planning branches from the root state.
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
        /// Recursively expands a role-based planning node into child branches.
        /// </summary>
        private void ExploreNode(
            PlanningGraphNode currentNode,
            WorldSnapshot worldSnapshot,
            List<PlanningBranch> branches,
            Dictionary<PlanningStateKey, PlanningBranchMetrics> bestMetricsByState)
        {
            if (currentNode.Depth >= MaxSearchDepth)
            {
                if (!HasUnresolvedPlanningSituation(currentNode.State, worldSnapshot))
                    AddLeafBranch(currentNode, branches);

                return;
            }

            IReadOnlyList<PlannedAction> candidates = _actionGenerator.Generate(currentNode.State, worldSnapshot);
            bool hasUnresolvedPlanningSituation = HasUnresolvedPlanningSituation(currentNode.State, worldSnapshot);
            if (candidates.Count == 0)
            {
                if (!hasUnresolvedPlanningSituation)
                    AddLeafBranch(currentNode, branches);

                return;
            }

            if (!hasUnresolvedPlanningSituation)
                AddLeafBranch(currentNode, branches);

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
        /// Checks whether an unresolved role-based planning situation remains for the projected state.
        /// </summary>
        private bool HasUnresolvedPlanningSituation(PlanningState planningState, WorldSnapshot worldSnapshot)
        {
            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return false;

            return _decisionPointDetector.TryDetect(
                planningState,
                projectedWorldSnapshot,
                out _);
        }

        /// <summary>
        /// Adds a leaf branch to the result list.
        /// </summary>
        private static void AddLeafBranch(PlanningGraphNode leafNode, List<PlanningBranch> branches)
        {
            if (leafNode == null)
                return;

            branches.Add(PlanningBranch.FromLeaf(leafNode));
        }

        /// <summary>
        /// Checks whether a known branch dominates a new node with the same state key.
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
        /// Checks whether the next state returns the branch to an ancestor state.
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
