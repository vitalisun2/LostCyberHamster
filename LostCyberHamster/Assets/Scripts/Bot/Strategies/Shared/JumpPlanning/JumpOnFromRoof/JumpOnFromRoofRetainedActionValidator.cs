using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранное roof-to-road jump-on action.
    /// </summary>
    internal sealed class JumpOnFromRoofRetainedActionValidator : IRetainedActionValidator
    {
        /// <summary>
        /// Политика runtime-различий конкретного варианта.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        /// <summary>
        /// Finder для повторного runtime-подтверждения сохранённого action.
        /// </summary>
        private readonly JumpOnFromRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Проверка применимости roof-to-road jump-on к найденной road target-chain.
        /// </summary>
        private readonly JumpOnFromRoofSpecification _specification;

        public JumpOnFromRoofRetainedActionValidator(
            IJumpOnFromRoofPolicy policy,
            JumpOnFromRoofFireWindowFinder fireWindowFinder,
            JumpOnFromRoofSpecification specification)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
            _specification = specification;
        }

        /// <summary>
        /// Возвращает тип action, который валидирует этот экземпляр.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, можно ли оставить ранее выбранный roof-to-road jump-on action.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет базовую совместимость context и action.
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            // Раскрывает context для дальнейших проверок.
            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            ObstacleSnapshot retainedTargetObstacle = context.TargetObstacle;
            PlannedAction action = context.Action;

            // Отсекает неполный context.
            if (planningState == null
                || projectedWorldSnapshot == null
                || retainedTargetObstacle == null)
            {
                return false;
            }

            // Получает runtime-дистанции действия.
            if (!_policy.TryGetTravel(out JumpOnFromRoofTravel travel))
                return false;

            // Берет актуальную road target-chain из decision point.
            DecisionPoint decisionPoint = context.DecisionPoint;
            if (decisionPoint?.Kind != DecisionPointKind.JumpOnFromRoofTarget
                || decisionPoint.Chain == null)
            {
                return false;
            }

            ObstacleChain actionChain = decisionPoint.Chain;
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out _))
            {
                return false;
            }

            // Повторяет specification-gate и сверяет target.
            if (!_specification.IsSatisfiedBy(
                    planningState,
                    actionChain,
                    lastRoof,
                    travel,
                    out ObstacleSnapshot currentTargetObstacle,
                    out int currentTargetIndex,
                    out _))
                return false;

            if (currentTargetObstacle.InstanceId != retainedTargetObstacle.InstanceId)
                return false;

            if (action.TargetObstacleInstanceId != currentTargetObstacle.InstanceId)
                return false;

            // Восстанавливает текущий fire shift.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    retainedTargetObstacle,
                    action,
                    out float fireShift))
                return false;

            // Сохраненный trigger — representative-точка внутри окна, а не жесткий planning-boundary.
            if (fireShift < 0f)
                fireShift = 0f;

            // Подтверждает outcome runtime resolver-ом и безопасность после удаления target.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            if (!_fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                    planningState.Hamster,
                    baseObstacles,
                    fireShift,
                    travel,
                    currentTargetIndex,
                    currentTargetObstacle.InstanceId))
                return false;

            return TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                planningState,
                projectedWorldSnapshot,
                currentTargetIndex,
                currentTargetObstacle.InstanceId,
                fireShift + travel.ActionTravel);
        }

        /// <summary>
        /// Вычисляет оставшийся fire shift относительно live trigger obstacle или target obstacle.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            out float fireShift)
        {
            // Отсекает неполные данные.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Ищет live trigger obstacle.
            float projectedTriggerX = action.TriggerX;
            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - projectedTriggerX;
                    return true;
                }

                if (triggerObstacleInstanceId != action.TargetObstacleInstanceId)
                {
                    fireShift = 0f;
                    return false;
                }
            }

            // Использует target как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
