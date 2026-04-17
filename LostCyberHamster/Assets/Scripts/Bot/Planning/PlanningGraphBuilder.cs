using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    public sealed class PlanningGraphBuilder
    {
        private const int MaxSearchDepth = 6;

        private readonly ActionGenerator _actionGenerator;
        private readonly TransitionSimulator _transitionSimulator;

        public PlanningGraphBuilder(ActionGenerator actionGenerator, TransitionSimulator transitionSimulator)
        {
            _actionGenerator = actionGenerator;
            _transitionSimulator = transitionSimulator;
        }

        public IReadOnlyList<PlanningBranch> BuildBranches(WorldSnapshot worldSnapshot, PlanningState rootState)
        {
            if (worldSnapshot == null || rootState == null)
                return Array.Empty<PlanningBranch>();

            var branches = new List<PlanningBranch>();
            PlanningGraphNode rootNode = PlanningGraphNode.CreateRoot(rootState);
            ExploreNode(rootNode, worldSnapshot, branches);
            return branches;
        }

        private void ExploreNode(
            PlanningGraphNode currentNode,
            WorldSnapshot worldSnapshot,
            List<PlanningBranch> branches)
        {
            // Stop expanding when the search reached the configured horizon.
            if (currentNode.Depth >= MaxSearchDepth)
            {
                AddLeafBranch(currentNode, branches);
                return;
            }

            // Expand all action variants available from the current projected state.
            IReadOnlyList<PlannedAction> candidates = _actionGenerator.Generate(currentNode.State, worldSnapshot);
            if (candidates.Count == 0)
            {
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

                ExploreNode(currentNode.CreateChild(nextState, candidate), worldSnapshot, branches);
                expandedAnyChild = true;
            }

            // Keep the current chain as a leaf when no candidate produced a valid new state.
            if (!expandedAnyChild)
                AddLeafBranch(currentNode, branches);
        }

        private static void AddLeafBranch(PlanningGraphNode leafNode, List<PlanningBranch> branches)
        {
            if (leafNode == null || leafNode.IsRoot)
                return;

            branches.Add(PlanningBranch.FromLeaf(leafNode));
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
