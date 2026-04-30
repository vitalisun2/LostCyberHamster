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

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Собирает компоненты super jump-over strategy.
    /// </summary>
    internal sealed class SuperJumpOverStrategy : IPlanningStrategy
    {
        private const string _superJumpClipName = "transform_super_jump";

        private readonly SuperJumpOverSpecification _specification;
        private readonly SuperJumpOverFireWindowFinder _fireWindowFinder;
        private readonly SuperJumpOverSimulator _simulator;

        public SuperJumpOverStrategy()
        {
            _specification = new SuperJumpOverSpecification();
            _fireWindowFinder = new SuperJumpOverFireWindowFinder();
            _simulator = new SuperJumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOverExecutor(triggerGate);
            RetainedValidator = new SuperJumpOverRetainedActionValidator();
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SuperJumpOver;
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

            if (!TryGetSuperJumpTravel(out float superJumpTravel))
            {
                return;
            }

            if (!_fireWindowFinder.TryFindFireMoment(
                    planningState,
                    worldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    superJumpTravel,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, fireShift, superJumpTravel));
        }

        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float superJumpTravel)
        {
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.SuperJumpOver,
                triggerX,
                renderWorldX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: SuperJumpOverSpecification.EnergyCost,
                description: $"Super jump over {targetObstacle.ObstacleType}");
        }

        /// <summary>
        /// Возвращает runtime distance super jump animation clip.
        /// </summary>
        private static bool TryGetSuperJumpTravel(out float superJumpTravel)
        {
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                Guard.ThrowIfNull((controller, nameof(TransformAnimatorController)));
                superJumpTravel = 0f;
                return false;
            }

            float clipTravel = HelpMethods.GetWorldShiftForClip(controller, _superJumpClipName);
            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();

            superJumpTravel = clipTravel + upgradeDelayTravel;
            return true;
        }

        /// <summary>
        /// Возвращает дополнительный world travel за задержку между первым jump input и upgrade в super jump.
        /// Берём середину допустимого double-jump окна как ожидаемый момент второго input.
        /// </summary>
        private static float GetSuperJumpUpgradeDelayTravel()
        {
            // Upgrade планируется примерно в середине double-jump window, а не на границе окна.
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
