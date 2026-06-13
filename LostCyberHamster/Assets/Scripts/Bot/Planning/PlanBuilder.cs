using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

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

            if (bestBranch == null || !bestBranch.HasActions)
            {
                return new PlanBuildResult(
                    BotPlan.Empty(worldSnapshot.ScreenRightEdgeX),
                    graphResult.DeadEndReport);
            }

            float score = _planEvaluator.Score(bestBranch.Actions);
            return new PlanBuildResult(
                new BotPlan(bestBranch.Actions, worldSnapshot.ScreenRightEdgeX, score),
                deadEndReport: null);
        }
    }
}
