using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Собирает компоненты SwitchLane strategy.
    /// </summary>
    internal sealed class SwitchLaneStrategy : IPlanningStrategy
    {
        private readonly SwitchLaneSpecification _specification;
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;
        private readonly SwitchLaneSimulator _simulator;

        public SwitchLaneStrategy()
        {
            _specification = new SwitchLaneSpecification();
            _fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _simulator = new SwitchLaneSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SwitchLaneExecutor(triggerGate);
            RetainedValidator = new SwitchLaneRetainedValidator(_specification, _fireWindowCalculator);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SwitchLane;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            if (actions == null)
                return;

            if (!_specification.IsSatisfiedBy(planningState, decisionPoint, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return;

            HamsterSnapshot hamster = planningState.Hamster;
            bool targetBottomLine = !hamster.IsOnBottomLine;
            if (!_fireWindowCalculator.TryGetLatestFireShift(hamster, targetObstacle, out float latestFireShift))
                return;

            IReadOnlyList<float> fireShifts = _fireWindowCalculator.CollectFireShifts(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift);
            for (int fireShiftIndex = 0; fireShiftIndex < fireShifts.Count; fireShiftIndex++)
                actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, targetBottomLine, fireShifts[fireShiftIndex]));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            bool targetBottomLine,
            float fireShift)
        {
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.SwitchLane,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + SwitchLaneTiming.DecisionTravel,
                postFireWorldShift: SwitchLaneTiming.DecisionTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine,
                energyCost: 0,
                description: $"Switch lane before {targetObstacle.ObstacleType}");
        }
    }
}
