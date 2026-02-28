using System.Collections.Generic;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Forward simulation planner: строит дерево решений на N шагов вперёд,
    /// оценивает листья через IStateEvaluator, возвращает лучший первый ход.
    /// Вся арифметика — никакой физики, ~0.1ms на кадр.
    /// </summary>
    public class BotPlanner
    {
        private readonly int _maxDepth;
        private readonly int _branchFactor;
        private readonly float _stepTimeSec;
        private readonly float _worldSpeed;
        private readonly IStateEvaluator _evaluator;

        private readonly IBotCommand[] _allCommands;

        public BotPlanner(
            int maxDepth = 3,
            int branchFactor = 5,
            float stepTimeSec = 0.3f,
            float worldSpeed = 3.8f,
            IStateEvaluator evaluator = null)
        {
            _maxDepth = maxDepth;
            _branchFactor = branchFactor;
            _stepTimeSec = stepTimeSec;
            _worldSpeed = worldSpeed;
            _evaluator = evaluator ?? new DefaultStateEvaluator();

            _allCommands = new IBotCommand[]
            {
                new BotCommands.DoNothingCommand(),
                new BotCommands.JumpCommand(),
                new BotCommands.SuperJumpCommand(),
                new BotCommands.SwitchLaneCommand(),
                new BotCommands.UseUltaCommand()
            };
        }

        public BotPlanner(BotStrategyConfig config, IStateEvaluator evaluator = null)
            : this(
                config.PlannerDepth,
                config.PlannerBranchFactor,
                stepTimeSec: 0.3f,
                worldSpeed: Consts.GameSpeedBase,
                evaluator: evaluator ?? new DefaultStateEvaluator(config))
        {
        }

        /// <summary>
        /// Просчитывает дерево решений и возвращает лучшее первое действие.
        /// </summary>
        public BotDecision Plan(SimWorldState rootState)
        {
            if (rootState.IsDead)
                return BotDecision.DoNothing("dead, cannot plan");

            float bestScore = float.MinValue;
            BotAction bestAction = BotAction.None;
            string bestReason = "no valid branches";

            // Для каждой доступной команды на первом шаге
            for (int c = 0; c < _allCommands.Length; c++)
            {
                var cmd = _allCommands[c];
                if (!cmd.CanExecute(ref rootState))
                    continue;

                var branch = rootState.Clone();
                cmd.Execute(ref branch);
                branch.Simulate(_stepTimeSec, _worldSpeed);

                float score = Search(ref branch, 1);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = cmd.Action;
                    bestReason = $"planned {cmd.Action}, score={score:F1}";
                }
            }

            if (bestAction == BotAction.None)
                return BotDecision.DoNothing(bestReason);

            return BotDecision.Planned(bestAction, bestReason, ConfidenceFromScore(bestScore));
        }

        // ──────────────── Tree Search ────────────────

        private float Search(ref SimWorldState state, int depth)
        {
            if (depth >= _maxDepth || state.IsDead)
                return _evaluator.Evaluate(ref state);

            float bestScore = float.MinValue;
            int branches = 0;

            for (int c = 0; c < _allCommands.Length && branches < _branchFactor; c++)
            {
                var cmd = _allCommands[c];
                if (!cmd.CanExecute(ref state))
                    continue;

                var branch = state.Clone();
                cmd.Execute(ref branch);
                branch.Simulate(_stepTimeSec, _worldSpeed);

                float score = Search(ref branch, depth + 1);
                if (score > bestScore)
                    bestScore = score;

                branches++;
            }

            // Если ни одна команда не доступна — оцениваем текущее состояние
            return bestScore > float.MinValue ? bestScore : _evaluator.Evaluate(ref state);
        }

        private static float ConfidenceFromScore(float score)
        {
            // Нормализуем score в 0-1 range для confidence
            if (score > 50f) return 1f;
            if (score > 20f) return 0.8f;
            if (score > 0f) return 0.6f;
            return 0.3f;
        }
    }
}
