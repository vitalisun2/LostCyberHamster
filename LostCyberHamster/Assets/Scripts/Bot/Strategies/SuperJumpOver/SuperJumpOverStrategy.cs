using System.Collections.Generic;
using System.Globalization;
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
        private const string SuperJumpClipName = "transform_super_jump";

        private static readonly Dictionary<string, float> _travelByCacheKey = new();

        private readonly SuperJumpOverSpecification _specification;
        private readonly SuperJumpOverFireWindowFinder _fireWindowFinder;
        private readonly SuperJumpOverScheduledFireShiftValidator _fireWindowValidator;
        private readonly SuperJumpOverSimulator _simulator;

        public SuperJumpOverStrategy()
        {
            _specification = new SuperJumpOverSpecification();
            _fireWindowFinder = new SuperJumpOverFireWindowFinder();
            _fireWindowValidator = new SuperJumpOverScheduledFireShiftValidator();
            _simulator = new SuperJumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOverExecutor(triggerGate);
            RetainedValidator = new SuperJumpOverRetainedActionValidator(_fireWindowValidator);
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
            return TryGetClipTravel(
                SuperJumpClipName,
                out superJumpTravel,
                DoubleJumpDetector.DoubleJumpThreshold * 0.5f * Assets.Scripts.Consts.GameSpeedBase,
                throwIfMissing: true);
        }

        /// <summary>
        /// Возвращает world shift для runtime animation clip.
        /// </summary>
        private static bool TryGetClipTravel(
            string clipName,
            out float travel,
            float extraTravel = 0f,
            bool throwIfMissing = false)
        {
            string cacheKey = BuildCacheKey(clipName, extraTravel);
            if (_travelByCacheKey.TryGetValue(cacheKey, out travel))
                return true;

            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                if (throwIfMissing)
                    Guard.ThrowIfNull((controller, nameof(TransformAnimatorController)));

                travel = 0f;
                return false;
            }

            travel = HelpMethods.GetWorldShiftForClip(controller, clipName) + extraTravel;
            _travelByCacheKey[cacheKey] = travel;
            return true;
        }

        /// <summary>
        /// Строит stable cache key для animation clip travel.
        /// </summary>
        private static string BuildCacheKey(string clipName, float extraTravel)
        {
            return clipName + ":" + extraTravel.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
