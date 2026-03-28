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
            if (snapshot == null || problem == null)
                return result;

            var obstacle = problem.SourceObstacle;
            string switchRejectReason = TryAddSwitchLaneCandidate(snapshot, problem, result, out bool hasSwitchLane);
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

        private string TryAddSwitchLaneCandidate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            List<BranchStep> result,
            out bool hasSwitchLane)
        {
            hasSwitchLane = _switchLaneStrategy.TryBuildStep(
                snapshot,
                problem,
                out BranchStep step,
                out string rejectReason);
            if (hasSwitchLane)
                result.Add(step);

            return rejectReason;
        }

        private void TryAddJumpCandidate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            List<BranchStep> result,
            out bool hasJump)
        {
            hasJump = false;
            if (!_jumpStrategy.TryBuildStep(snapshot, problem, _projectedWorld, out BranchStep step, out _))
                return;

            result.Add(step);
            hasJump = true;
        }
    }
}
