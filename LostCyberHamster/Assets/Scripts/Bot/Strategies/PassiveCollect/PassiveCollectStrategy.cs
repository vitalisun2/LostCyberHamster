using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Common;

using Assets.Scripts.Bot.Diagnostics;
namespace Assets.Scripts.Bot.Strategies.PassiveCollect
{
    /// <summary>
    /// Собирает no-input кандидаты подбора полезных collectables.
    /// </summary>
    internal sealed class PassiveCollectStrategy : IPlanningStrategy
    {
        public PassiveCollectStrategy()
        {
            Executor = new PassiveCollectExecutor(new LiveObstacleResolver());
            Simulator = new PassiveCollectSimulator();
        }

        public BotActionKind ActionKind => BotActionKind.PassiveCollect;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет, есть ли на текущей линии collectable для no-input pickup.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.HasContext(planningState, decisionPoint)
                && PlanningStrategyApplicability.CanPlanPassiveCollect(planningState.Hamster)
                && PlanningStrategyApplicability.IsCurrentLane(planningState, decisionPoint)
                && PlanningStrategyApplicability.HasRole(decisionPoint, ObstacleRole.Collectible);
        }

        /// <summary>
        /// Добавляет passive collect action, если collectable можно безопасно подобрать без input.
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

            if (!PassiveCollectPlanner.TryBuildModel(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    out PassiveCollectModel model))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            if (BotDiagnostics.IsEnabled(BotDiagnosticCategory.Strategy, BotDiagnosticLevel.Verbose))
            {
                BotDiagnostics.Log(
                    BotDiagnosticCategory.Strategy,
                    BotDiagnosticLevel.Verbose,
                    $"[Bot PLAN] PassiveCollect candidate kind={model.ObjectiveValue.Kind} " +
                    $"value={model.ObjectiveValue.EffectiveGain} target={model.TargetCollectible.ObstacleType} " +
                    $"targetIndex={model.TargetCollectibleIndex} shift={model.CompletionWorldShift:F2}");
            }

            return PlanningStrategyResult.FromAction(BuildAction(model));
        }

        private static PlannedAction BuildAction(PassiveCollectModel model)
        {
            ObstacleSnapshot target = model.TargetCollectible;
            return new PlannedAction(
                BotActionKind.PassiveCollect,
                target.LeftX,
                renderWorldX: target.LeftX,
                completionWorldShift: model.CompletionWorldShift,
                postFireWorldShift: model.CompletionWorldShift,
                model.TargetCollectibleIndex,
                targetObstacleInstanceId: target.InstanceId,
                triggerObstacleInstanceId: target.InstanceId,
                energyCost: 0,
                description: $"Passive collect {model.ObjectiveValue.Kind} {target.ObstacleType}",
                collectibleObjectiveValue: model.ObjectiveValue);
        }
    }
}
