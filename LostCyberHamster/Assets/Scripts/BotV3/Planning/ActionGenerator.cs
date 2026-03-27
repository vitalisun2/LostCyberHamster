using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Генерирует возможные действия для видимых объектов.
    /// Работает как реестр action-specific стратегий planner'а.
    /// </summary>
    public class ActionGenerator
    {
        private readonly ProjectedWorld _projectedWorld = new ProjectedWorld();
        private readonly IActionStrategy[] _strategies;

        public ActionGenerator()
        {
            _strategies = new IActionStrategy[]
            {
                new SwitchLaneStrategy(SwitchLaneTimingMode.Earliest),
                new SwitchLaneStrategy(SwitchLaneTimingMode.Latest),
                new JumpStrategy()
            };
        }

        public List<BranchStep> Generate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            string logScope = null)
        {
            var result = new List<BranchStep>();
            if (snapshot == null || problem?.SourceObstacle == null)
                return result;

            var obstacle = problem.SourceObstacle;
            bool hasSwitchLane = false;
            bool hasJump = false;
            string switchRejectReason = null;

            for (int s = 0; s < _strategies.Length; s++)
            {
                var strategy = _strategies[s];
                if (!strategy.CanSolve(problem))
                    continue;

                if (!strategy.TryBuildStep(snapshot, problem, _projectedWorld, out BranchStep step, out string rejectReason))
                {
                    if (strategy.Action == BotAction.SwitchLane)
                        switchRejectReason = rejectReason;

                    continue;
                }

                result.Add(step);
                if (step.Action == BotAction.SwitchLane) hasSwitchLane = true;
                if (step.Action == BotAction.Jump) hasJump = true;
            }

            if (hasSwitchLane || hasJump)
                BotLogger.LogActionCandidates(obstacle, hasSwitchLane, hasJump, switchRejectReason, snapshot, logScope);

            return result;
        }

        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            for (int i = 0; i < _strategies.Length; i++)
            {
                if (_strategies[i].Action == step.Action)
                {
                    return _strategies[i].Project(snapshot, step, _projectedWorld);
                }
            }

            return new StepProjectionResult
            {
                IsSafe = false,
                DebugReason = $"No strategy for action {step.Action}"
            };
        }
    }
}
