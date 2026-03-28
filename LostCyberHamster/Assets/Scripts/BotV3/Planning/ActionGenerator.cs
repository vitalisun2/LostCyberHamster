using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Генерирует шаги только для одной текущей проблемы.
    /// Делегирует построение и проекцию конкретным IActionStrategy.
    /// </summary>
    public class ActionGenerator
    {
        private readonly ProjectedWorld _projectedWorld = new ProjectedWorld();
        private readonly List<IActionStrategy> _strategies;
        private readonly Dictionary<BotAction, IActionStrategy> _strategyByAction;

        public ActionGenerator()
        {
            _strategies = new List<IActionStrategy>
            {
                new SwitchLaneStrategy(),
                new JumpStrategy(),
            };

            _strategyByAction = new Dictionary<BotAction, IActionStrategy>();
            foreach (var strategy in _strategies)
                _strategyByAction[strategy.Action] = strategy;
        }

        public List<BranchStep> Generate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            string logScope = null)
        {
            var result = new List<BranchStep>();
            if (snapshot == null || problem == null)
                return result;

            var added = new Dictionary<BotAction, bool>();
            var rejectReasons = new Dictionary<BotAction, string>();

            foreach (var strategy in _strategies)
            {
                bool success = strategy.TryBuildStep(
                    snapshot, problem, _projectedWorld,
                    out BranchStep step, out string rejectReason);
                added[strategy.Action] = success;
                rejectReasons[strategy.Action] = rejectReason;
                if (success)
                    result.Add(step);
            }

            if (result.Count > 0)
            {
                added.TryGetValue(BotAction.SwitchLane, out bool hasSwitchLane);
                added.TryGetValue(BotAction.Jump, out bool hasJump);
                rejectReasons.TryGetValue(BotAction.SwitchLane, out string switchRejectReason);
                BotLogger.LogActionCandidates(
                    problem.SourceObstacle, hasSwitchLane, hasJump,
                    switchRejectReason, snapshot, logScope);
            }

            return result;
        }

        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            if (_strategyByAction.TryGetValue(step.Action, out var strategy))
                return strategy.Project(snapshot, step, _projectedWorld);

            return new StepProjectionResult
            {
                IsSafe = false,
                DebugReason = $"No strategy for action {step.Action}"
            };
        }
    }
}
