using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Результат построения bot plan.
    /// </summary>
    internal sealed class PlanBuildResult
    {
        public PlanBuildResult(BotPlan plan, PlanningDeadEndReport deadEndReport)
        {
            Plan = plan ?? BotPlan.Empty();
            DeadEndReport = deadEndReport;
        }

        public BotPlan Plan { get; }
        public PlanningDeadEndReport DeadEndReport { get; }
        public bool HasDeadEnd => DeadEndReport != null;
    }

    /// <summary>
    /// Собирает role-based план с нуля по текущему snapshot мира.
    /// </summary>
    public sealed class PlanBuilder
    {
        private readonly PlanningGraphBuilder _graphBuilder;
        private readonly PlanEvaluator _planEvaluator;

        /// <summary>
        /// Создает role-based сборщик плана поверх generator, simulator и evaluator.
        /// </summary>
        public PlanBuilder(
            ActionGenerator actionGenerator,
            TransitionSimulator transitionSimulator,
            PlanEvaluator planEvaluator)
        {
            _graphBuilder = new PlanningGraphBuilder(actionGenerator, transitionSimulator);
            _planEvaluator = planEvaluator;
        }

        /// <summary>
        /// Строит role-based план по текущему snapshot мира.
        /// </summary>
        internal PlanBuildResult Build(WorldSnapshot worldSnapshot)
        {
            if (worldSnapshot == null)
                return new PlanBuildResult(BotPlan.Empty(), deadEndReport: null);

            return Build(worldSnapshot, PlanningState.FromSnapshot(worldSnapshot));
        }

        /// <summary>
        /// Строит role-based план по snapshot мира от указанного planning-состояния.
        /// </summary>
        internal PlanBuildResult Build(WorldSnapshot worldSnapshot, PlanningState rootState)
        {
            if (worldSnapshot == null)
                return new PlanBuildResult(BotPlan.Empty(), deadEndReport: null);

            if (rootState == null)
            {
                return new PlanBuildResult(
                    BotPlan.Empty(worldSnapshot.ScreenRightEdgeX),
                    deadEndReport: null);
            }

            // Разворачивает planning tree от переданного root-состояния.
            PlanningGraphBuildResult graphResult = _graphBuilder.BuildBranches(worldSnapshot, rootState);
            PlanningBranch bestBranch = _planEvaluator.SelectBest(graphResult.Branches);
            LogGroundJumpOverBranchSelection(rootState, graphResult.Branches, bestBranch);
            LogLowEnergySwitchBranchSelection(
                rootState,
                graphResult.Branches,
                graphResult.DeadEndBranches,
                bestBranch);
            LogSuperJumpOnBranchSelection(rootState, graphResult.Branches, bestBranch);
            LogRoofExitSwitchBranchSelection(
                rootState,
                graphResult.Branches,
                graphResult.DeadEndBranches,
                bestBranch);
            LogRoofRouteChoiceBranchSelection(
                rootState,
                graphResult.Branches,
                graphResult.DeadEndBranches,
                bestBranch);
            LogJumpOverBoundaryBranchSelection(rootState, graphResult.Branches, graphResult.DeadEndBranches, bestBranch);
            LogLowEnergyPickupRouteSelection(
                rootState,
                graphResult.Branches,
                graphResult.DeadEndBranches,
                bestBranch);

            // Пустая successful-ветка означает "продолжать бег" и должна выигрывать у dead-end fallback.
            if (bestBranch != null)
                return BuildSuccessfulResult(worldSnapshot, bestBranch);

            PlanningDeadEndBranch bestDeadEndBranch = _planEvaluator.SelectBestDeadEnd(graphResult.DeadEndBranches);
            LogDeadEndFallbackBranchSelection(rootState, graphResult.DeadEndBranches, bestDeadEndBranch);
            LogRoofExitSwitchDeadEndSelection(rootState, graphResult.DeadEndBranches, bestDeadEndBranch);
            LogJumpFromRoofDeadEndSelection(rootState, graphResult.DeadEndBranches, bestDeadEndBranch);
            LogSwitchLaneDeadEndSelection(rootState, graphResult.DeadEndBranches, bestDeadEndBranch);
            LogLowEnergyPickupDeadEndSelection(rootState, graphResult.DeadEndBranches, bestDeadEndBranch);
            if (bestDeadEndBranch?.Branch != null && bestDeadEndBranch.Branch.HasActions)
                return BuildDeadEndFallbackResult(worldSnapshot, bestDeadEndBranch);

            return new PlanBuildResult(
                BotPlan.Empty(worldSnapshot.ScreenRightEdgeX),
                bestDeadEndBranch?.Report);
        }

        /// <summary>
        /// Создает результат по полноценной успешной ветке.
        /// </summary>
        private PlanBuildResult BuildSuccessfulResult(WorldSnapshot worldSnapshot, PlanningBranch branch)
        {
            float score = _planEvaluator.Score(branch.Actions);
            LogPassiveAdvanceIntoRoofEntry(branch);
            return new PlanBuildResult(
                new BotPlan(branch.Actions, worldSnapshot.ScreenRightEdgeX, score),
                deadEndReport: null);
        }

        /// <summary>
        /// Создает fallback-план из safe-prefix ветки, которая уперлась в dead-end.
        /// </summary>
        private PlanBuildResult BuildDeadEndFallbackResult(
            WorldSnapshot worldSnapshot,
            PlanningDeadEndBranch deadEndBranch)
        {
            PlanningBranch branch = deadEndBranch.Branch;
            float score = _planEvaluator.Score(branch.Actions);
            return new PlanBuildResult(
                new BotPlan(branch.Actions, worldSnapshot.ScreenRightEdgeX, score),
                deadEndBranch.Report);
        }

        private static void LogGroundJumpOverBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            PlanningBranch selectedBranch)
        {
            if (rootState?.Hamster == null || branches == null || branches.Count == 0)
                return;

            if (!ShouldLogGroundJumpOverBranches(branches, selectedBranch))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot GROUND_JUMP_BRANCHES] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidates={FormatBranches(branches)}");
        }

        private static bool ShouldLogGroundJumpOverBranches(
            IReadOnlyList<PlanningBranch> branches,
            PlanningBranch selectedBranch)
        {
            if (selectedBranch?.Actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < selectedBranch.Actions.Count; actionIndex++)
            {
                PlannedAction action = selectedBranch.Actions[actionIndex];
                if (action?.Kind == BotActionKind.SuperJumpOver && IsSmallObstacleOverAction(action))
                    return true;
            }

            return false;
        }

        private static bool IsSmallObstacleOverAction(PlannedAction action)
        {
            if (action?.Description == null)
                return false;

            return action.Description.Contains("smallNotAliveRoad")
                || action.Description.Contains("smallAlive");
        }

        private static void LogLowEnergySwitchBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningBranch selectedBranch)
        {
            if (rootState?.Hamster == null || selectedBranch?.Actions == null)
                return;

            if (rootState.Hamster.Energy > 15 || !HasSwitchBeforeRoadAndRoof(selectedBranch))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot LOW_ENERGY_SWITCH_BRANCHES] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidateCount={FormatCount(branches)} " +
                $"deadEndCount={FormatCount(deadEndBranches)}");
        }

        private static bool HasSwitchBeforeRoadAndRoof(PlanningBranch branch)
        {
            if (branch?.Actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < branch.Actions.Count; actionIndex++)
            {
                PlannedAction action = branch.Actions[actionIndex];
                if (action?.Kind == BotActionKind.SwitchLane
                    && action.Description != null
                    && action.Description.Contains("smallNotAliveRoadAndRoof"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogDeadEndFallbackBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningDeadEndBranch selectedDeadEndBranch)
        {
            if (rootState?.Hamster == null || selectedDeadEndBranch?.Branch == null)
                return;

            PlanningBranch branch = selectedDeadEndBranch.Branch;
            if (!HasSmallGroundSuperJumpOver(branch) && !HasSwitchBeforeRoadAndRoof(branch))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot DEAD_END_FALLBACK_BRANCHES] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatDeadEndBranch(selectedDeadEndBranch)} " +
                $"deadEndCount={FormatCount(deadEndBranches)}");
        }

        private static void LogPassiveAdvanceIntoRoofEntry(PlanningBranch branch)
        {
            if (!HasPassiveAdvanceIntoRoofEntry(branch))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot PASSIVE_ADVANCE_ROOF_BRANCH] " +
                $"finalNext={branch.FinalNextObstacleIndex} " +
                $"finalProjection={branch.FinalProjectionWorldShift:F2} " +
                $"selected={FormatBranch(branch)}");
        }

        private static void LogRoofExitSwitchBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningBranch selectedBranch)
        {
            if (!HasRoofExitIntoSwitch(selectedBranch))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot ROOF_EXIT_SWITCH_BRANCHES] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidateCount={FormatCount(branches)} " +
                $"deadEndCount={FormatCount(deadEndBranches)}");
        }

        private static void LogRoofRouteChoiceBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningBranch selectedBranch)
        {
            if (rootState?.Hamster == null
                || selectedBranch?.Actions == null
                || selectedBranch.Actions.Count == 0
                || rootState.Hamster.HamsterState != Assets.Scripts.Gameplay.Enums.HamsterStateEnum.RoofRun
                || selectedBranch.Actions[0].Kind != BotActionKind.PassiveRoofExit)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot ROOF_ROUTE_CHOICE] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidates={FormatRelevantRoofRouteBranches(branches)} " +
                $"deadEnds={FormatRelevantRoofRouteDeadEnds(deadEndBranches)}");
        }

        private static void LogJumpOverBoundaryBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningBranch selectedBranch)
        {
            if (rootState?.Hamster == null
                || !StartsWithJumpOverIntoSwitch(selectedBranch)
                || !HasActionKind(selectedBranch, BotActionKind.PassiveRoofExit))
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot JUMP_OVER_BOUNDARY_BRANCH] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidateCount={FormatCount(branches)} " +
                $"deadEndCount={FormatCount(deadEndBranches)}");
        }

        private static void LogSuperJumpOnBranchSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            PlanningBranch selectedBranch)
        {
            if (rootState?.Hamster == null
                || !HasLeadingSmallAliveSuperJumpOn(selectedBranch))
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot SUPER_JUMPON_BRANCHES] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidates={FormatBranches(branches)}");
        }

        private static void LogLowEnergyPickupRouteSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningBranch> branches,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningBranch selectedBranch)
        {
            if (rootState?.Hamster == null || rootState.Hamster.Energy > 45)
                return;

            if (!HasEnergyCollectibleAction(selectedBranch)
                && !HasEnergyCollectibleBranch(branches)
                && !HasEnergyCollectibleDeadEnd(deadEndBranches))
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot LOW_ENERGY_PICKUP_ROUTE] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatBranch(selectedBranch)} " +
                $"candidates={FormatBranches(branches)} " +
                $"deadEnds={FormatDeadEndBranches(deadEndBranches)}");
        }

        private static void LogRoofExitSwitchDeadEndSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningDeadEndBranch selectedDeadEndBranch)
        {
            if (!HasRoofExitIntoSwitch(selectedDeadEndBranch?.Branch))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot ROOF_EXIT_SWITCH_DEAD_END] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatDeadEndBranch(selectedDeadEndBranch)} " +
                $"deadEndCount={FormatCount(deadEndBranches)}");
        }

        private static void LogJumpFromRoofDeadEndSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningDeadEndBranch selectedDeadEndBranch)
        {
            if (rootState?.Hamster == null
                || !HasActionKind(selectedDeadEndBranch?.Branch, BotActionKind.JumpFromRoof))
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot JUMP_FROM_ROOF_DEAD_END] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatDeadEndBranch(selectedDeadEndBranch)} " +
                $"deadEnds={FormatDeadEndBranches(deadEndBranches)}");
        }

        private static void LogSwitchLaneDeadEndSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningDeadEndBranch selectedDeadEndBranch)
        {
            if (rootState?.Hamster == null
                || selectedDeadEndBranch?.Branch?.Actions == null
                || selectedDeadEndBranch.Branch.Actions.Count != 1
                || selectedDeadEndBranch.Branch.Actions[0]?.Kind != BotActionKind.SwitchLane)
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot SWITCH_ONLY_DEAD_END] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatDeadEndBranch(selectedDeadEndBranch)} " +
                $"deadEnds={FormatDeadEndBranches(deadEndBranches)}");
        }

        private static void LogLowEnergyPickupDeadEndSelection(
            PlanningState rootState,
            IReadOnlyList<PlanningDeadEndBranch> deadEndBranches,
            PlanningDeadEndBranch selectedDeadEndBranch)
        {
            if (rootState?.Hamster == null || rootState.Hamster.Energy > 45)
                return;

            if (!StartsWithSwitchJumpPassive(selectedDeadEndBranch?.Branch)
                && !HasEnergyCollectibleDeadEnd(deadEndBranches)
                && !HasInsufficientEnergyReason(selectedDeadEndBranch?.Report))
            {
                return;
            }

            BotDiagnostics.Log(
                BotDiagnosticCategory.BranchSelection,
                BotDiagnosticLevel.Verbose,
                "[Bot LOW_ENERGY_PICKUP_DEAD_END] " +
                $"lane={(rootState.IsOnBottomLine ? "bottom" : "top")} " +
                $"state={rootState.Hamster.HamsterState} " +
                $"energy={rootState.Hamster.Energy} " +
                $"nextObstacleIndex={rootState.NextObstacleIndex} " +
                $"projection={rootState.ProjectionWorldShift:F2} " +
                $"selected={FormatDeadEndBranch(selectedDeadEndBranch)} " +
                $"deadEnds={FormatDeadEndBranches(deadEndBranches)}");
        }

        private static bool HasRoofExitIntoSwitch(PlanningBranch branch)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            if (actions == null || actions.Count < 2)
                return false;

            for (int actionIndex = 0; actionIndex < actions.Count - 1; actionIndex++)
            {
                if (actions[actionIndex]?.Kind == BotActionKind.PassiveRoofExit
                    && actions[actionIndex + 1]?.Kind == BotActionKind.SwitchLane)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StartsWithJumpOverIntoSwitch(PlanningBranch branch)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            return actions != null
                && actions.Count >= 2
                && actions[0]?.Kind == BotActionKind.JumpOver
                && actions[1]?.Kind == BotActionKind.SwitchLane;
        }

        private static bool HasActionKind(PlanningBranch branch, BotActionKind actionKind)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            if (actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                if (actions[actionIndex]?.Kind == actionKind)
                    return true;
            }

            return false;
        }

        private static bool HasLeadingRoofToRoofCandidate(IReadOnlyList<PlanningBranch> branches)
        {
            if (branches == null)
                return false;

            for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
            {
                PlannedAction firstAction = GetFirstAction(branches[branchIndex]);
                if (IsRoofToRoofAction(firstAction?.Kind ?? default))
                    return true;
            }

            return false;
        }

        private static string FormatRelevantRoofRouteBranches(IReadOnlyList<PlanningBranch> branches)
        {
            if (branches == null || branches.Count == 0)
                return "none";

            const int maxBranches = 12;
            var parts = new List<string>(maxBranches + 1);
            for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
            {
                PlanningBranch branch = branches[branchIndex];
                PlannedAction firstAction = GetFirstAction(branch);
                if (!IsRoofRouteChoiceAction(firstAction?.Kind ?? default))
                    continue;

                parts.Add($"{branchIndex}:{FormatBranch(branch)}");
                if (parts.Count >= maxBranches)
                    break;
            }

            if (parts.Count == 0)
                return "none";

            if (parts.Count < branches.Count)
                parts.Add($"totalCandidates={branches.Count}");

            return string.Join(" || ", parts);
        }

        private static string FormatRelevantRoofRouteDeadEnds(IReadOnlyList<PlanningDeadEndBranch> deadEndBranches)
        {
            if (deadEndBranches == null || deadEndBranches.Count == 0)
                return "none";

            const int maxBranches = 8;
            var parts = new List<string>(maxBranches + 1);
            for (int branchIndex = 0; branchIndex < deadEndBranches.Count; branchIndex++)
            {
                PlanningDeadEndBranch deadEndBranch = deadEndBranches[branchIndex];
                PlannedAction firstAction = GetFirstAction(deadEndBranch?.Branch);
                if (!IsRoofRouteChoiceAction(firstAction?.Kind ?? default))
                    continue;

                parts.Add($"{branchIndex}:{FormatDeadEndBranch(deadEndBranch)}");
                if (parts.Count >= maxBranches)
                    break;
            }

            if (parts.Count == 0)
                return "none";

            if (parts.Count < deadEndBranches.Count)
                parts.Add($"totalDeadEnds={deadEndBranches.Count}");

            return string.Join(" || ", parts);
        }

        private static PlannedAction GetFirstAction(PlanningBranch branch)
        {
            return branch?.Actions != null && branch.Actions.Count > 0
                ? branch.Actions[0]
                : null;
        }

        private static bool IsRoofRouteChoiceAction(BotActionKind actionKind)
        {
            return actionKind == BotActionKind.PassiveRoofExit
                || actionKind == BotActionKind.RoofSwitchLane
                || IsRoofToRoofAction(actionKind);
        }

        private static bool IsRoofToRoofAction(BotActionKind actionKind)
        {
            return actionKind == BotActionKind.JumpFromRoofOnRoof
                || actionKind == BotActionKind.SuperJumpFromRoofOnRoof;
        }

        private static bool HasLeadingSmallAliveSuperJumpOn(PlanningBranch branch)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            if (actions == null || actions.Count == 0)
                return false;

            PlannedAction firstAction = actions[0];
            return firstAction?.Kind == BotActionKind.SuperJumpOn
                && firstAction.Description != null
                && firstAction.Description.Contains("smallAlive");
        }

        private static bool HasPassiveAdvanceIntoRoofEntry(PlanningBranch branch)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            if (actions == null || actions.Count < 2)
                return false;

            for (int actionIndex = 0; actionIndex < actions.Count - 1; actionIndex++)
            {
                if (actions[actionIndex]?.Kind == BotActionKind.PassiveAdvance
                    && IsRoofEntryAction(actions[actionIndex + 1]?.Kind ?? default))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRoofEntryAction(BotActionKind actionKind)
        {
            return actionKind == BotActionKind.JumpOnRoof
                || actionKind == BotActionKind.SuperJumpOnRoof;
        }

        private static bool HasSmallGroundSuperJumpOver(PlanningBranch branch)
        {
            if (branch?.Actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < branch.Actions.Count; actionIndex++)
            {
                PlannedAction action = branch.Actions[actionIndex];
                if (action?.Kind == BotActionKind.SuperJumpOver && IsSmallObstacleOverAction(action))
                    return true;
            }

            return false;
        }

        private static bool HasEnergyCollectibleBranch(IReadOnlyList<PlanningBranch> branches)
        {
            if (branches == null)
                return false;

            for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
            {
                if (HasEnergyCollectibleAction(branches[branchIndex]))
                    return true;
            }

            return false;
        }

        private static bool HasEnergyCollectibleDeadEnd(IReadOnlyList<PlanningDeadEndBranch> deadEndBranches)
        {
            if (deadEndBranches == null)
                return false;

            for (int branchIndex = 0; branchIndex < deadEndBranches.Count; branchIndex++)
            {
                if (HasEnergyCollectibleAction(deadEndBranches[branchIndex]?.Branch))
                    return true;
            }

            return false;
        }

        private static bool HasEnergyCollectibleAction(PlanningBranch branch)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            if (actions == null)
                return false;

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                if (actions[actionIndex]?.CollectibleObjectiveValue.Kind == CollectibleKind.Energy)
                    return true;
            }

            return false;
        }

        private static bool StartsWithSwitchJumpPassive(PlanningBranch branch)
        {
            IReadOnlyList<PlannedAction> actions = branch?.Actions;
            return actions != null
                && actions.Count >= 3
                && actions[0]?.Kind == BotActionKind.SwitchLane
                && actions[1]?.Kind == BotActionKind.JumpOn
                && actions[2]?.Kind == BotActionKind.PassiveAdvance;
        }

        private static bool HasInsufficientEnergyReason(PlanningDeadEndReport report)
        {
            IReadOnlyList<StrategyDeadEndReason> reasons = report?.Reasons;
            if (reasons == null)
                return false;

            for (int reasonIndex = 0; reasonIndex < reasons.Count; reasonIndex++)
            {
                string message = reasons[reasonIndex]?.Message;
                if (message != null && message.Contains("Недостаточно энергии"))
                    return true;
            }

            return false;
        }

        private static string FormatBranches(IReadOnlyList<PlanningBranch> branches)
        {
            if (branches == null || branches.Count == 0)
                return "none";

            const int maxBranches = 8;
            int count = branches.Count < maxBranches ? branches.Count : maxBranches;
            var parts = new List<string>(count + 1);
            for (int branchIndex = 0; branchIndex < count; branchIndex++)
                parts.Add($"{branchIndex}:{FormatBranch(branches[branchIndex])}");

            if (branches.Count > maxBranches)
                parts.Add($"... total={branches.Count}");

            return string.Join(" || ", parts);
        }

        private static string FormatCount<T>(IReadOnlyCollection<T> items)
        {
            return items == null ? "none" : items.Count.ToString();
        }

        private static string FormatDeadEndBranches(IReadOnlyList<PlanningDeadEndBranch> deadEndBranches)
        {
            if (deadEndBranches == null || deadEndBranches.Count == 0)
                return "none";

            const int maxBranches = 6;
            int count = deadEndBranches.Count < maxBranches ? deadEndBranches.Count : maxBranches;
            var parts = new List<string>(count + 1);
            for (int branchIndex = 0; branchIndex < count; branchIndex++)
                parts.Add($"{branchIndex}:{FormatDeadEndBranch(deadEndBranches[branchIndex])}");

            if (deadEndBranches.Count > maxBranches)
                parts.Add($"... total={deadEndBranches.Count}");

            return string.Join(" || ", parts);
        }

        private static string FormatDeadEndBranch(PlanningDeadEndBranch deadEndBranch)
        {
            if (deadEndBranch == null)
                return "none";

            PlanningDeadEndReport report = deadEndBranch.Report;
            return
                $"{FormatBranch(deadEndBranch.Branch)} " +
                $"reportDepth={FormatNullable(report?.Depth)} " +
                $"reportNext={FormatNullable(report?.NextObstacleIndex)} " +
                $"reportProjection={FormatNullable(report?.ProjectionWorldShift)} " +
                $"reasons={FormatDeadEndReasons(report?.Reasons)}";
        }

        private static string FormatBranch(PlanningBranch branch)
        {
            if (branch == null)
                return "none";

            return $"metrics={FormatMetrics(branch.Metrics)} actions={FormatActions(branch.Actions)}";
        }

        private static string FormatMetrics(PlanningBranchMetrics metrics)
        {
            if (metrics == null)
                return "none";

            return
                $"cost={metrics.EnergyCost},beforeMajor={metrics.EnergyBeforeFirstMajor}," +
                $"major={metrics.MajorObjectiveCount},elim={metrics.ImmediateTargetEliminationCount}," +
                $"bypass={metrics.ImmediateObstacleBypassEnergyCost},energy={metrics.EnergyCollectibleValue}," +
                $"actions={metrics.ActionCount}";
        }

        private static string FormatActions(IReadOnlyList<PlannedAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return "none";

            var parts = new List<string>(actions.Count);
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                PlannedAction action = actions[actionIndex];
                if (action == null)
                    continue;

                parts.Add(
                    $"{action.Kind}/cost={action.EnergyCost}/target={FormatNullable(action.TargetObstacleInstanceId)}" +
                    $"/trigger={FormatNullable(action.TriggerObstacleInstanceId)}" +
                    $"/complete={action.CompletionWorldShift:F2}" +
                    $"/postFire={action.PostFireWorldShift:F2}" +
                    $"/desc={action.Description}");
            }

            return parts.Count == 0 ? "none" : string.Join(">", parts);
        }

        private static string FormatDeadEndReasons(IReadOnlyList<StrategyDeadEndReason> reasons)
        {
            if (reasons == null || reasons.Count == 0)
                return "none";

            const int maxReasons = 4;
            int count = reasons.Count < maxReasons ? reasons.Count : maxReasons;
            var parts = new List<string>(count + 1);
            for (int reasonIndex = 0; reasonIndex < count; reasonIndex++)
            {
                StrategyDeadEndReason reason = reasons[reasonIndex];
                if (reason == null)
                    continue;

                parts.Add($"{reason.StrategyName}:{reason.Message}");
            }

            if (reasons.Count > maxReasons)
                parts.Add($"... total={reasons.Count}");

            return parts.Count == 0 ? "none" : string.Join(";", parts);
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }

        private static string FormatNullable(float? value)
        {
            return value.HasValue ? value.Value.ToString("F2") : "none";
        }
    }
}
