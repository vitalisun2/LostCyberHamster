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

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Собирает компоненты обычного jump-over strategy.
    /// </summary>
    internal sealed class JumpOverStrategy : IPlanningStrategy
    {
        private const string JumpClipName = "transform_jump";

        private static readonly Dictionary<string, float> _travelByCacheKey = new();

        private readonly JumpOverSpecification _specification;
        private readonly JumpOverFireWindowFinder _fireWindowFinder;
        private readonly JumpOverScheduledFireShiftValidator _fireWindowValidator;
        private readonly JumpOverSimulator _simulator;

        /// <summary>
        /// Создаёт strategy и её runtime/planning компоненты.
        /// </summary>
        public JumpOverStrategy()
        {
            _specification = new JumpOverSpecification();
            _fireWindowFinder = new JumpOverFireWindowFinder();
            _fireWindowValidator = new JumpOverScheduledFireShiftValidator();
            _simulator = new JumpOverSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new JumpOverExecutor(triggerGate);
            RetainedValidator = new JumpOverRetainedActionValidator(_fireWindowValidator);
            Simulator = _simulator;
        }

        /// <summary>
        /// Тип действия, которое планирует strategy.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOver;

        /// <summary>
        /// Runtime executor для выполнения запланированного jump-over.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Validator для сохранения уже выбранного jump-over action между planning ticks.
        /// </summary>
        public IRetainedActionValidator RetainedValidator { get; }

        /// <summary>
        /// Simulator для прогноза состояния после jump-over.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет jump-over action, если текущая decision point подходит под эту strategy.
        /// </summary>
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
                    targetObstacle,
                    targetObstacleIndex,
                    jumpTravel,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, fireShift, jumpTravel));
        }

        /// <summary>
        /// Создаёт planned action для найденного fire shift.
        /// </summary>
        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float jumpTravel)
        {
            float triggerX = targetObstacle.LeftX - fireShift;
            float renderWorldX = triggerX + planningState.ProjectionWorldShift;

            return new PlannedAction(
                BotActionKind.JumpOver,
                triggerX,
                renderWorldX,
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
        private static bool TryGetJumpTravel(out float travel)
        {
            return TryGetClipTravel(JumpClipName, out travel);
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
