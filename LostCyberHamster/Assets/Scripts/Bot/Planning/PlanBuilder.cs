using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
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
        public BotPlan Build(WorldSnapshot worldSnapshot)
        {
            if (worldSnapshot == null)
                return BotPlan.Empty();

            return Build(worldSnapshot, PlanningState.FromSnapshot(worldSnapshot));
        }

        /// <summary>
        /// Строит role-based план по snapshot мира от указанного planning-состояния.
        /// </summary>
        public BotPlan Build(WorldSnapshot worldSnapshot, PlanningState rootState)
        {
            if (worldSnapshot == null)
                return BotPlan.Empty();

            if (rootState == null)
                return BotPlan.Empty(worldSnapshot.ScreenRightEdgeX);

            // Разворачивает planning tree от переданного root-состояния.
            IReadOnlyList<PlanningBranch> branches = _graphBuilder.BuildBranches(worldSnapshot, rootState);
            PlanningBranch bestBranch = _planEvaluator.SelectBest(branches);

            if (bestBranch == null || !bestBranch.HasActions)
                return BotPlan.Empty(worldSnapshot.ScreenRightEdgeX);

            float score = _planEvaluator.Score(bestBranch.Actions);
            return new BotPlan(bestBranch.Actions, worldSnapshot.ScreenRightEdgeX, score);
        }
    }
}
