using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Результат построения role-based графа планирования.
    /// </summary>
    internal sealed class PlanningGraphBuildResult
    {
        public PlanningGraphBuildResult(
            IReadOnlyList<PlanningBranch> branches,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches)
        {
            Branches = branches ?? Array.Empty<PlanningBranch>();
            DeadEndBranches = deadEndBranches ?? Array.Empty<PlanningDeadEndBranch>();
        }

        public IReadOnlyList<PlanningBranch> Branches { get; }
        public IReadOnlyList<PlanningDeadEndBranch> DeadEndBranches { get; }
    }

    /// <summary>
    /// Описывает unresolved участок, где применимые стратегии не создали action.
    /// </summary>
    internal sealed class PlanningDeadEndReport
    {
        public PlanningDeadEndReport(
            int depth,
            int nextObstacleIndex,
            float projectionWorldShift,
            IReadOnlyList<StrategyDeadEndReason> reasons)
        {
            Depth = depth;
            NextObstacleIndex = nextObstacleIndex;
            ProjectionWorldShift = projectionWorldShift;
            Reasons = reasons ?? Array.Empty<StrategyDeadEndReason>();
        }

        public int Depth { get; }
        public int NextObstacleIndex { get; }
        public float ProjectionWorldShift { get; }
        public IReadOnlyList<StrategyDeadEndReason> Reasons { get; }
    }

    /// <summary>
    /// Хранит безопасный prefix ветки, который упёрся в unresolved dead-end.
    /// </summary>
    internal sealed class PlanningDeadEndBranch
    {
        public PlanningDeadEndBranch(PlanningBranch branch, PlanningDeadEndReport report)
        {
            Branch = branch;
            Report = report;
        }

        public PlanningBranch Branch { get; }
        public PlanningDeadEndReport Report { get; }
    }

    /// <summary>
    /// Строит role-based дерево решений для текущего planning-состояния.
    /// </summary>
    public sealed class PlanningGraphBuilder
    {
        private const int MaxSearchDepth = 6;

        private readonly ActionGenerator _actionGenerator;
        private readonly TransitionSimulator _transitionSimulator;

        /// <summary>
        /// Создает builder role-based графа поверх генератора действий и simulator-а переходов.
        /// </summary>
        public PlanningGraphBuilder(ActionGenerator actionGenerator, TransitionSimulator transitionSimulator)
        {
            _actionGenerator = actionGenerator;
            _transitionSimulator = transitionSimulator;
        }

        /// <summary>
        /// Строит все достижимые role-based ветки планирования от корневого состояния.
        /// </summary>
        internal PlanningGraphBuildResult BuildBranches(WorldSnapshot worldSnapshot, PlanningState rootState)
        {
            if (worldSnapshot == null || rootState == null)
            {
                return new PlanningGraphBuildResult(
                    Array.Empty<PlanningBranch>(),
                    deadEndBranches: null);
            }

            var branches = new List<PlanningBranch>();
            var deadEndBranches = new List<PlanningDeadEndBranch>();
            var bestNodesByState = new Dictionary<PlanningStateKey, PlanningGraphNode>();
            PlanningGraphNode rootNode = PlanningGraphNode.CreateRoot(rootState);

            ExploreNode(rootNode, worldSnapshot, branches, deadEndBranches, bestNodesByState);

            return new PlanningGraphBuildResult(branches, deadEndBranches);
        }

        /// <summary>
        /// Рекурсивно раскрывает role-based узел планирования в дочерние ветки.
        /// </summary>
        private void ExploreNode(
            PlanningGraphNode currentNode,
            WorldSnapshot worldSnapshot,
            List<PlanningBranch> branches,
            List<PlanningDeadEndBranch> deadEndBranches,
            Dictionary<PlanningStateKey, PlanningGraphNode> bestNodesByState)
        {
            if (ShouldPruneSameState(currentNode, bestNodesByState))
                return;

            if (currentNode.Depth >= MaxSearchDepth)
            {
                AddLeafBranch(currentNode, branches);
                return;
            }

            ActionGenerationResult generationResult = _actionGenerator.Generate(currentNode.State, worldSnapshot);

            IReadOnlyList<PlannedAction> candidates = generationResult.Actions;

            if (candidates.Count == 0)
            {
                if (!generationResult.HasUnresolvedPlanningSituation)
                {
                    AddLeafBranch(currentNode, branches);
                    return;
                }

                if (generationResult.HasDeadEndReasons)
                    AddDeadEndBranch(currentNode, generationResult, deadEndBranches);

                return;
            }

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                PlannedAction candidate = candidates[candidateIndex];
                if (candidate == null)
                    continue;

                if (IsRedundantSwitchLaneContinuation(currentNode, candidate))
                    continue;

                PlanningState nextState = _transitionSimulator.Simulate(
                    currentNode.State,
                    candidate,
                    worldSnapshot);
                if (nextState == null)
                    continue;

                PlanningGraphNode childNode = currentNode.CreateChild(nextState, candidate);
                ExploreNode(childNode, worldSnapshot, branches, deadEndBranches, bestNodesByState);
            }
        }

        /// <summary>
        /// Отсекает node, если тот же planning-state уже достигнут не худшей prefix-веткой.
        /// </summary>
        private static bool ShouldPruneSameState(
            PlanningGraphNode currentNode,
            Dictionary<PlanningStateKey, PlanningGraphNode> bestNodesByState)
        {
            if (currentNode == null || bestNodesByState == null)
                return false;

            if (!bestNodesByState.TryGetValue(currentNode.StateKey, out PlanningGraphNode existingNode))
            {
                bestNodesByState.Add(currentNode.StateKey, currentNode);
                return false;
            }

            if (ReferenceEquals(existingNode, currentNode))
                return false;

            PlanningBranch existingBranch = PlanningBranch.FromLeaf(existingNode);
            PlanningBranch currentBranch = PlanningBranch.FromLeaf(currentNode);
            if (PlanningBranchComparer.IsBetterOrEqual(existingBranch, currentBranch))
                return true;

            bestNodesByState[currentNode.StateKey] = currentNode;
            return false;
        }

        /// <summary>
        /// Создает report для первого unresolved node без доступных actions.
        /// </summary>
        private static PlanningDeadEndReport BuildDeadEndReport(
            PlanningGraphNode currentNode,
            ActionGenerationResult generationResult)
        {
            return new PlanningDeadEndReport(
                currentNode.Depth,
                currentNode.State.NextObstacleIndex,
                currentNode.State.ProjectionWorldShift,
                generationResult.DeadEndReasons);
        }

        /// <summary>
        /// Добавляет leaf-ветку в результат.
        /// </summary>
        private static void AddLeafBranch(PlanningGraphNode leafNode, List<PlanningBranch> branches)
        {
            if (leafNode == null)
                return;

            branches.Add(PlanningBranch.FromLeaf(leafNode));
        }

        /// <summary>
        /// Добавляет safe-prefix ветку, которая дошла до unresolved dead-end.
        /// </summary>
        private static void AddDeadEndBranch(
            PlanningGraphNode deadEndNode,
            ActionGenerationResult generationResult,
            List<PlanningDeadEndBranch> deadEndBranches)
        {
            if (deadEndNode == null || deadEndBranches == null)
                return;

            deadEndBranches.Add(new PlanningDeadEndBranch(
                PlanningBranch.FromLeaf(deadEndNode),
                BuildDeadEndReport(deadEndNode, generationResult)));
        }

        /// <summary>
        /// Отсекает switch-lane ping-pong, который возвращает ветку к той же или более ранней ситуации.
        /// </summary>
        private static bool IsRedundantSwitchLaneContinuation(
            PlanningGraphNode currentNode,
            PlannedAction candidate)
        {
            PlannedAction previousAction = currentNode?.IncomingAction;
            if (previousAction == null
                || previousAction.Kind != BotActionKind.SwitchLane
                || candidate == null
                || candidate.Kind != BotActionKind.SwitchLane)
            {
                return false;
            }

            if (previousAction.IsOppositeLaneEntry)
                return true;

            return candidate.TargetObstacleIndex <= previousAction.TargetObstacleIndex;
        }

    }
}
