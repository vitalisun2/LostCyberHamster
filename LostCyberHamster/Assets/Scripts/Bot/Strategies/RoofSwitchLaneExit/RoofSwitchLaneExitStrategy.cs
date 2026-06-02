using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Планирует безопасный сход с крыши через смену линии.
    /// </summary>
    internal sealed class RoofSwitchLaneExitStrategy : IPlanningStrategy
    {
        private readonly RoofSwitchLaneExitPolicy _policy;
        private readonly RoofSwitchLaneExitPlanner _planner;

        public RoofSwitchLaneExitStrategy()
        {
            _policy = new RoofSwitchLaneExitPolicy();
            var fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _planner = new RoofSwitchLaneExitPlanner(fireWindowCalculator);

            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());
            Executor = new RoofSwitchLaneExitExecutor(_policy, triggerGate);
            RetainedValidator = new RoofSwitchLaneExitRetainedActionValidator(_policy, _planner);
            Simulator = new RoofSwitchLaneExitSimulator(_policy);
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        public IActionExecutionHandler Executor { get; }

        public IRetainedActionValidator RetainedValidator { get; }

        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет безопасные кандидаты для схода с крыши через смену линии.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            if (actions == null)
                return;

            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return;

            IReadOnlyList<RoofSwitchLaneExitModel> models = _planner.CollectModels(
                planningState,
                worldSnapshot,
                decisionPoint,
                runFromRoofTravel,
                GetSelectionRatios());

            for (int modelIndex = 0; modelIndex < models.Count; modelIndex++)
                actions.Add(BuildAction(planningState, models[modelIndex]));
        }

        /// <summary>
        /// Создаёт запланированное действие для выбранной модели схода с крыши через смену линии.
        /// </summary>
        private PlannedAction BuildAction(
            PlanningState planningState,
            RoofSwitchLaneExitModel model)
        {
            float fireShift = model.FireShift;
            float projectedTriggerX = model.ContextObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                model.FireWindowSample.FirstFireShift,
                model.FireWindowSample.LastFireShift);

            return new PlannedAction(
                _policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: model.CompletionWorldShift,
                postFireWorldShift: model.RunFromRoofTravel,
                model.ContextObstacleIndex,
                targetObstacleInstanceId: model.ContextObstacle.InstanceId,
                targetBottomLine: model.TargetBottomLine,
                energyCost: 0,
                description: $"{_policy.DescriptionPrefix} before {model.ContextObstacle.ObstacleType}",
                triggerWindow: triggerWindow);
        }

        /// <summary>
        /// Возвращает доли выборки для проверки раннего и среднего окна запуска.
        /// </summary>
        private static IReadOnlyList<float> GetSelectionRatios()
        {
            return new[]
            {
                SwitchLaneTiming.EarlyWindowSelectionRatio,
                SwitchLaneTiming.MidWindowSelectionRatio
            };
        }
    }
}
