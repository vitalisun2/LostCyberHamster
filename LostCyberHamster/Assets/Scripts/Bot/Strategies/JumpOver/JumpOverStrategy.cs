using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Собирает компоненты обычного jump-over strategy.
    /// </summary>
    internal sealed class JumpOverStrategy : IPlanningStrategy
    {
        private const string _jumpClipName = "transform_jump";

        private readonly JumpOverSpecification _specification;
        private readonly JumpOverFireWindowFinder _fireWindowFinder;
        private readonly JumpOverSimulator _simulator;

        public JumpOverStrategy()
        {
            _specification = new JumpOverSpecification();
            _fireWindowFinder = new JumpOverFireWindowFinder();
            _simulator = new JumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOverExecutor(triggerGate);
            RetainedValidator = new JumpOverRetainedActionValidator();
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.JumpOver;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            if (!_specification.IsSatisfiedBy(planningState, decisionPoint, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return;

            if (!TryGetJumpTravel(out float jumpTravel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    jumpTravel,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, fireShift, jumpTravel));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float jumpTravel)
        {
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.JumpOver,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + jumpTravel,
                postFireWorldShift: jumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: JumpOverSpecification.EnergyCost,
                description: $"Jump over {targetObstacle.ObstacleType}");
        }

        /// <summary>
        /// Возвращает runtime distance обычного jump animation clip.
        /// </summary>
        private static bool TryGetJumpTravel(out float jumpTravel)
        {
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                jumpTravel = 0f;
                return false;
            }

            jumpTravel = HelpMethods.GetWorldShiftForClip(controller, _jumpClipName);
            return true;
        }
    }
}
