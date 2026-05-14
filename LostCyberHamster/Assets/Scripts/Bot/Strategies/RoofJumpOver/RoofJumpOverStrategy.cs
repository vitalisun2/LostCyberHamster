using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Строит действия roof jump over над опасным small obstacle на текущей крыше.
    /// </summary>
    internal sealed class RoofJumpOverStrategy : IPlanningStrategy
    {
        private const string _roofJumpOverClipName = "transform_roof_jump";
        private const string _jumpFromRoofClipName = "transform_jump_from_roof";
        private const string _mediumRoofJumpOverClipName = "transform_medium_roof_jump";
        private const string _mediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        private readonly RoofJumpOverSpecification _specification;
        private readonly RoofJumpOverFireWindowFinder _fireWindowFinder;
        private readonly RoofJumpOverSimulator _simulator;

        public RoofJumpOverStrategy()
        {
            _specification = new RoofJumpOverSpecification();
            _fireWindowFinder = new RoofJumpOverFireWindowFinder();
            _simulator = new RoofJumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new RoofJumpOverExecutor(triggerGate);
            RetainedValidator = new RoofJumpOverRetainedActionValidator();
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.RoofJumpOver;
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

            if (!_specification.IsSatisfiedBy(
                    planningState,
                    decisionPoint,
                    worldSnapshot,
                    out ObstacleSnapshot hazardObstacle,
                    out int hazardObstacleIndex,
                    out ObstacleSnapshot supportObstacle,
                    out _))
                return;

            if (!TryGetRoofJumpOverTravel(out float roofJumpOverTravel, out float jumpFromRoofTravel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    hazardObstacle,
                    supportObstacle,
                    roofJumpOverTravel,
                    jumpFromRoofTravel,
                    out float fireShift))
                return;

            actions.Add(BuildAction(
                planningState,
                hazardObstacle,
                hazardObstacleIndex,
                supportObstacle,
                fireShift,
                roofJumpOverTravel));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot hazardObstacle,
            int hazardObstacleIndex,
            ObstacleSnapshot supportObstacle,
            float fireShift,
            float roofJumpOverTravel)
        {
            float projectedTriggerX = hazardObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.RoofJumpOver,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + roofJumpOverTravel,
                postFireWorldShift: roofJumpOverTravel,
                hazardObstacleIndex,
                targetObstacleInstanceId: hazardObstacle.InstanceId,
                triggerObstacleInstanceId: hazardObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: RoofJumpOverSpecification.EnergyCost,
                description: $"Roof jump over {hazardObstacle.ObstacleType}",
                resultRoofSupportInstanceId: supportObstacle.InstanceId);
        }

        internal static bool TryGetRoofJumpOverTravel(out float roofJumpOverTravel, out float jumpFromRoofTravel)
        {
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                roofJumpOverTravel = 0f;
                jumpFromRoofTravel = 0f;
                return false;
            }

            roofJumpOverTravel = HelpMethods.GetWorldShiftForClip(controller, _roofJumpOverClipName);
            jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, _jumpFromRoofClipName);
            if (roofJumpOverTravel <= 0f)
                roofJumpOverTravel = HelpMethods.GetWorldShiftForClip(controller, _mediumRoofJumpOverClipName);

            if (jumpFromRoofTravel <= 0f)
                jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, _mediumJumpFromRoofClipName);

            return roofJumpOverTravel > 0f && jumpFromRoofTravel > 0f;
        }
    }
}
