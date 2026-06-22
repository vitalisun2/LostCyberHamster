using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.PassiveAdvance
{
    /// <summary>
    /// Добавляет no-input переход, который безопасно продвигает planning до следующей useful situation.
    /// </summary>
    internal sealed class PassiveAdvanceStrategy : IPlanningStrategy
    {
        public PassiveAdvanceStrategy()
        {
            Executor = new PassiveAdvanceExecutor(new LiveObstacleResolver());
            Simulator = new PassiveAdvanceSimulator();
        }

        public BotActionKind ActionKind => BotActionKind.PassiveAdvance;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет, нужен ли no-input advance для opposite-lane ситуации.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.HasContext(planningState, decisionPoint)
                && PlanningStrategyApplicability.CanPlanGroundRun(planningState.Hamster)
                && PlanningStrategyApplicability.IsOppositeLane(planningState, decisionPoint)
                && decisionPoint.Chain.HasAnyRequiredPlanningRole();
        }

        /// <summary>
        /// Создает passive advance action для safe ожидания ухода opposite-lane blocker.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            if (!PassiveAdvancePlanner.TryBuildModel(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    out PassiveAdvanceModel model))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            return PlanningStrategyResult.FromAction(BuildAction(model));
        }

        private static PlannedAction BuildAction(PassiveAdvanceModel model)
        {
            ObstacleSnapshot boundaryObstacle = model.BoundaryObstacle;
            return new PlannedAction(
                BotActionKind.PassiveAdvance,
                boundaryObstacle.LeftX,
                renderWorldX: boundaryObstacle.LeftX,
                completionWorldShift: model.CompletionWorldShift,
                postFireWorldShift: model.CompletionWorldShift,
                model.BoundaryObstacleIndex,
                targetObstacleInstanceId: boundaryObstacle.InstanceId,
                triggerObstacleInstanceId: boundaryObstacle.InstanceId,
                energyCost: 0,
                description: $"Passive advance past {boundaryObstacle.ObstacleType}");
        }
    }
}
