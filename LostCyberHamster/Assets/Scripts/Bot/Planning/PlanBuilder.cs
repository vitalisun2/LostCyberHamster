using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Собирает итоговый план из committed-префикса и лучшей новой ветки.
    /// </summary>
    public sealed class PlanBuilder
    {
        private readonly PlanningGraphBuilder _graphBuilder;
        private readonly TransitionSimulator _transitionSimulator;
        private readonly PlanEvaluator _planEvaluator;

        /// <summary>
        /// Создает сборщик плана поверх генератора, симулятора и evaluator'а.
        /// </summary>
        public PlanBuilder(
            ActionGenerator actionGenerator,
            TransitionSimulator transitionSimulator,
            PlanEvaluator planEvaluator)
        {
            _graphBuilder = new PlanningGraphBuilder(actionGenerator, transitionSimulator);
            _transitionSimulator = transitionSimulator;
            _planEvaluator = planEvaluator;
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
                if (!ShouldRetainAction(action, actionIndex, worldSnapshot, retainInProgressHead))
                    break;

                PlanningState nextState = retainInProgressHead && actionIndex == 0
                    ? ProjectInProgressHead(currentState, action, worldSnapshot)
                    : _transitionSimulator.Simulate(currentState, action, worldSnapshot);
                if (nextState == null)
                    break;

                retainedActions.Add(action);
                currentState = nextState;
            }

            return currentState;
        }

        private static PlanningState ProjectInProgressHead(
            PlanningState planningState,
            PlannedAction action,
            WorldSnapshot worldSnapshot)
        {
            if (planningState == null || action == null || worldSnapshot == null)
                return null;

            float remainingPostFireShift = action.PostFireWorldShift;
            if (action.TargetObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != action.TargetObstacleInstanceId.Value)
                        continue;

                    float shiftSinceFire = action.TriggerX - obstacle.LeftX;
                    if (shiftSinceFire > 0f)
                        remainingPostFireShift = action.PostFireWorldShift - shiftSinceFire;

                    break;
                }
            }

            if (remainingPostFireShift < 0f)
                remainingPostFireShift = 0f;

            HamsterSnapshot hamster = planningState.Hamster;
            HamsterSnapshot nextHamster = action.Kind switch
            {
                BotActionKind.Jump => new HamsterSnapshot(
                    HamsterStateEnum.Run,
                    hamster.IsOnBottomLine,
                    isOnRoof: false,
                    hamster.Energy,
                    hamster.Lives,
                    hamster.IsDamaged,
                    isShifting: false,
                    roofSupportInstanceId: null,
                    hamster.HamsterLeftX,
                    hamster.HamsterRightX),
                BotActionKind.Tap => new HamsterSnapshot(
                    hamster.HamsterState,
                    hamster.IsOnBottomLine,
                    isOnRoof: false,
                    hamster.Energy,
                    hamster.Lives,
                    hamster.IsDamaged,
                    isShifting: false,
                    hamster.RoofSupportInstanceId,
                    hamster.HamsterLeftX,
                    hamster.HamsterRightX),
                _ => hamster
            };

            float nextProjectionWorldShift = planningState.ProjectionWorldShift + remainingPostFireShift;
            int nextObstacleIndex = worldSnapshot.Obstacles.Count;
            for (int obstacleIndex = planningState.NextObstacleIndex; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                float projectedRightX = obstacle.RightX - nextProjectionWorldShift;
                if (projectedRightX > nextHamster.HamsterLeftX)
                {
                    nextObstacleIndex = obstacleIndex;
                    break;
                }
            }

            return new PlanningState(
                nextHamster,
                nextObstacleIndex,
                nextProjectionWorldShift);
        }

        private static bool ShouldRetainAction(
            PlannedAction action,
            int actionIndex,
            WorldSnapshot worldSnapshot,
            bool retainInProgressHead)
        {
            if (retainInProgressHead && actionIndex == 0)
                return true;

            return action.RenderWorldX >= worldSnapshot.ScreenLeftEdgeX
                && action.RenderWorldX <= worldSnapshot.ScreenRightEdgeX;
        }

        private static float GetCommittedBoundaryX(BotPlan committedPlan, WorldSnapshot worldSnapshot)
        {
            if (committedPlan != null && committedPlan.CommittedBoundaryX > 0f)
                return committedPlan.CommittedBoundaryX;

            return worldSnapshot != null ? worldSnapshot.ScreenRightEdgeX : 0f;
        }
    }
}
