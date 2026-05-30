using Assets.Scripts.Bot.Strategies.Shared.Simulation;
using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Собирает итоговый план из committed-префикса и лучшей новой ветки.
    /// </summary>
    public sealed class PlanBuilder
    {
        private const int InProgressExecutionHandoffActionCount = 2;

        private readonly PlanningGraphBuilder _graphBuilder;
        private readonly TransitionSimulator _transitionSimulator;
        private readonly PlanEvaluator _planEvaluator;
        private readonly RetainedActionRevalidator _retainedActionRevalidator;
        private readonly ActionInProgressProjector _inProgressProjector;

        /// <summary>
        /// Создает сборщик плана поверх генератора, симулятора и evaluator'а.
        /// </summary>
        public PlanBuilder(
            ActionGenerator actionGenerator,
            TransitionSimulator transitionSimulator,
            PlanEvaluator planEvaluator,
            RetainedActionRevalidator retainedActionRevalidator,
            ActionInProgressProjector inProgressProjector)
        {
            _graphBuilder = new PlanningGraphBuilder(actionGenerator, transitionSimulator);
            _transitionSimulator = transitionSimulator;
            _planEvaluator = planEvaluator;
            _retainedActionRevalidator = retainedActionRevalidator;
            _inProgressProjector = inProgressProjector;
        }

        /// <summary>
        /// Строит новый план по текущему snapshot мира и остаткам старого плана.
        /// </summary>
        public BotPlan Build(WorldSnapshot worldSnapshot, BotPlan committedPlan, bool retainInProgressHead = false)
        {
            if (worldSnapshot == null)
                return BotPlan.Empty(committedPlan?.CommittedBoundaryX ?? 0f);

            var actions = new List<PlannedAction>();
            PlanningState rootState = PlanningState.FromSnapshot(worldSnapshot);
            PlanningState tailRootState = ProjectCommittedPrefix(
                worldSnapshot,
                committedPlan,
                rootState,
                actions,
                retainInProgressHead);

            // Expand only the tail beyond the committed prefix.
            IReadOnlyList<PlanningBranch> branches = _graphBuilder.BuildBranches(worldSnapshot, tailRootState);
            PlanningBranch bestBranch = _planEvaluator.SelectBest(branches);

            if (bestBranch != null && bestBranch.HasActions)
            {
                for (int actionIndex = 0; actionIndex < bestBranch.Actions.Count; actionIndex++)
                    actions.Add(bestBranch.Actions[actionIndex]);
            }

            if (actions.Count == 0)
                return BotPlan.Empty(GetCommittedBoundaryX(committedPlan, worldSnapshot));

            float score = _planEvaluator.Score(actions);
            return new BotPlan(actions, worldSnapshot.ScreenRightEdgeX, score);
        }

        /// <summary>
        /// Проецирует committed-префикс плана.
        /// </summary>
        private PlanningState ProjectCommittedPrefix(
            WorldSnapshot worldSnapshot,
            BotPlan committedPlan,
            PlanningState rootState,
            List<PlannedAction> retainedActions,
            bool retainInProgressHead)
        {
            if (committedPlan == null || !committedPlan.HasActions)
                return rootState;

            PlanningState currentState = rootState;
            IReadOnlyList<PlannedAction> currentActions = committedPlan.Actions;

            for (int actionIndex = 0; actionIndex < currentActions.Count; actionIndex++)
            {
                PlannedAction action = currentActions[actionIndex];
                if (!ShouldRetainCommittedAction(action, actionIndex, worldSnapshot, retainInProgressHead))
                    break;

                bool isExecutionHandoffAction = IsInProgressExecutionHandoffAction(
                    actionIndex,
                    retainInProgressHead);
                bool isBoundaryRetainedAction = IsBoundaryRetainedAction(
                    currentActions,
                    actionIndex,
                    worldSnapshot,
                    retainInProgressHead);
                bool requiresRetainedValidation = isBoundaryRetainedAction
                    || IsTargetBoundJumpOnBeyondScreen(action, worldSnapshot);
                if (requiresRetainedValidation
                    && !isExecutionHandoffAction
                    && !_retainedActionRevalidator.IsStillValid(currentState, action, worldSnapshot))
                    break;

                PlanningState nextState = retainInProgressHead && actionIndex == 0
                    ? _inProgressProjector.Project(currentState, action, worldSnapshot)
                    : _transitionSimulator.Simulate(currentState, action, worldSnapshot);
                if (nextState == null)
                    break;

                retainedActions.Add(action);
                currentState = nextState;
            }

            return currentState;
        }

        /// <summary>
        /// Проверяет границу retained-префикса.
        /// </summary>
        private static bool IsBoundaryRetainedAction(
            IReadOnlyList<PlannedAction> actions,
            int actionIndex,
            WorldSnapshot worldSnapshot,
            bool retainInProgressHead)
        {
            if (actions == null)
                return false;

            int nextActionIndex = actionIndex + 1;
            if (nextActionIndex >= actions.Count)
                return true;

            return !ShouldRetainAction(
                actions[nextActionIndex],
                nextActionIndex,
                worldSnapshot,
                retainInProgressHead);
        }

        /// <summary>
        /// Проверяет сохранение committed-action в префиксе нового плана.
        /// </summary>
        private static bool ShouldRetainCommittedAction(
            PlannedAction action,
            int actionIndex,
            WorldSnapshot worldSnapshot,
            bool retainInProgressHead)
        {
            if (IsInProgressExecutionHandoffAction(actionIndex, retainInProgressHead))
                return true;

            return ShouldRetainAction(action, actionIndex, worldSnapshot, retainInProgressHead);
        }

        /// <summary>
        /// Определяет атомарный handoff между уже запущенным head-action и ближайшим следующим действием.
        /// </summary>
        private static bool IsInProgressExecutionHandoffAction(int actionIndex, bool retainInProgressHead)
        {
            return retainInProgressHead && actionIndex < InProgressExecutionHandoffActionCount;
        }

        /// <summary>
        /// Проверяет сохранение действия в префиксе.
        /// </summary>
        private static bool ShouldRetainAction(
            PlannedAction action,
            int actionIndex,
            WorldSnapshot worldSnapshot,
            bool retainInProgressHead)
        {
            if (retainInProgressHead && actionIndex == 0)
                return true;

            if (IsTargetBoundJumpOnAction(action))
            {
                return action.RenderWorldX >= worldSnapshot.ScreenLeftEdgeX
                    && action.RenderWorldX <= worldSnapshot.VisionRightEdgeX;
            }

            return action.RenderWorldX >= worldSnapshot.ScreenLeftEdgeX
                && action.RenderWorldX <= worldSnapshot.ScreenRightEdgeX;
        }

        /// <summary>
        /// Проверяет, является ли действие target-bound jump-on вариантом.
        /// </summary>
        private static bool IsTargetBoundJumpOnAction(PlannedAction action)
        {
            if (action == null || !action.TargetObstacleInstanceId.HasValue)
                return false;

            return action.Kind == BotActionKind.JumpOn
                || action.Kind == BotActionKind.SuperJumpOn
                || action.Kind == BotActionKind.JumpOnFromRoof
                || action.Kind == BotActionKind.SuperJumpOnFromRoof;
        }

        /// <summary>
        /// Проверяет, требует ли сохраненный jump-on повторной валидации за экранной границей.
        /// </summary>
        private static bool IsTargetBoundJumpOnBeyondScreen(
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            return IsTargetBoundJumpOnAction(action)
                && action.RenderWorldX > worldSnapshot.ScreenRightEdgeX;
        }

        /// <summary>
        /// Возвращает committed boundary X.
        /// </summary>
        private static float GetCommittedBoundaryX(BotPlan committedPlan, WorldSnapshot worldSnapshot)
        {
            if (committedPlan != null && committedPlan.CommittedBoundaryX > 0f)
                return committedPlan.CommittedBoundaryX;

            return worldSnapshot != null ? worldSnapshot.ScreenRightEdgeX : 0f;
        }
    }
}
