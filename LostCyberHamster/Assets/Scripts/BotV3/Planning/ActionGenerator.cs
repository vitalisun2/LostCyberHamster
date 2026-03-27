using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Генерирует шаги только для одной текущей проблемы.
    /// Здесь находится orchestration между problem, action strategies и логированием кандидатов.
    /// </summary>
    public class ActionGenerator
    {
        private readonly ProjectedWorld _projectedWorld = new ProjectedWorld();
        private readonly SwitchLaneStrategy _switchLaneStrategy = new SwitchLaneStrategy();
        private readonly JumpStrategy _jumpStrategy = new JumpStrategy();

        public List<BranchStep> Generate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            string logScope = null)
        {
            var result = new List<BranchStep>();
            if (snapshot == null || problem?.SourceObstacle == null)
                return result;

            var obstacle = problem.SourceObstacle;
            string switchRejectReason = TryAddSwitchLaneCandidates(snapshot, problem, result, out bool hasSwitchLane);
            TryAddJumpCandidate(snapshot, problem, result, out bool hasJump);

            if (hasSwitchLane || hasJump)
                BotLogger.LogActionCandidates(obstacle, hasSwitchLane, hasJump, switchRejectReason, snapshot, logScope);

            return result;
        }

        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            switch (step.Action)
            {
                case BotAction.SwitchLane:
                    return _switchLaneStrategy.Project(snapshot, step, _projectedWorld);

                case BotAction.Jump:
                    return _jumpStrategy.Project(snapshot, step, _projectedWorld);

                default:
                    return new StepProjectionResult
                    {
                        IsSafe = false,
                        DebugReason = $"No strategy for action {step.Action}"
                    };
            }
        }

        private string TryAddSwitchLaneCandidates(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            List<BranchStep> result,
            out bool hasSwitchLane)
        {
            hasSwitchLane = false;
            string rejectReason = null;

            if (!_switchLaneStrategy.CanSolve(problem))
                return rejectReason;

            if (TryAddSwitchLaneCandidate(snapshot, problem, SwitchLaneTimingMode.Earliest, result, out string earliestRejectReason))
                hasSwitchLane = true;
            else
                rejectReason = earliestRejectReason;

            if (TryAddSwitchLaneCandidate(snapshot, problem, SwitchLaneTimingMode.Latest, result, out string latestRejectReason))
                hasSwitchLane = true;
            else if (rejectReason == null)
                rejectReason = latestRejectReason;

            return rejectReason;
        }

        private bool TryAddSwitchLaneCandidate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            SwitchLaneTimingMode timingMode,
            List<BranchStep> result,
            out string rejectReason)
        {
            if (!_switchLaneStrategy.TryBuildStep(
                    snapshot,
                    problem,
                    timingMode,
                    out BranchStep step,
                    out rejectReason))
                return false;

            result.Add(step);
            return true;
        }

        private void TryAddJumpCandidate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            List<BranchStep> result,
            out bool hasJump)
        {
            hasJump = false;
            if (!_jumpStrategy.CanSolve(problem))
                return;

            if (!_jumpStrategy.TryBuildStep(snapshot, problem, _projectedWorld, out BranchStep step, out _))
                return;

            result.Add(step);
            hasJump = true;
        }
    }
}
