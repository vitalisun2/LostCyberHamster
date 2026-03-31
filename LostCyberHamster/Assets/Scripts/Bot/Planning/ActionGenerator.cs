using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Генерирует шаги только для одной текущей проблемы.
    /// Делегирует построение и проекцию конкретным IActionStrategy.
    /// </summary>
    public class ActionGenerator
    {
        private readonly ProjectedWorld _projectedWorld = new ProjectedWorld();
        private readonly Dictionary<BotAction, IActionStrategy> _strategyByAction;

        public ActionGenerator()
        {
            var strategies = new IActionStrategy[]
            {
                new SwitchLaneStrategy(),
                new JumpStrategy(),
                new SuperJumpStrategy(),
            };

            _strategyByAction = new Dictionary<BotAction, IActionStrategy>();
            foreach (var strategy in strategies)
                _strategyByAction[strategy.Action] = strategy;
        }

        /// <summary>
        /// Генерирует шаги-кандидаты для заданной проблемы, опрашивая все стратегии.
        /// </summary>
        public List<BranchStep> Generate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            string logScope = null)
        {
            var result = new List<BranchStep>();
            if (snapshot == null || problem == null)
                return result;

            // Опросить каждую стратегию
            var candidates = new List<(BotAction action, bool added, string rejectReason)>();

            foreach (var strategy in _strategyByAction.Values)
            {
                bool success = strategy.TryBuildStep(
                    snapshot, problem, _projectedWorld,
                    out BranchStep step, out string rejectReason);
                candidates.Add((strategy.Action, success, rejectReason));
                if (success)
                    result.Add(step);
            }

            // Залогировать результаты
            if (result.Count > 0)
                LogCandidates(problem.SourceObstacle, candidates, snapshot, logScope);

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

        private static void LogCandidates(
            ObstacleInfo obstacle,
            List<(BotAction action, bool added, string rejectReason)> candidates,
            BotSceneSnapshot snapshot,
            string logScope)
        {
            BotLogger.LogActionCandidates(obstacle, candidates, snapshot, logScope);
        }
    }
}
