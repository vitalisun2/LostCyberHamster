using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранное ground jump-on action.
    /// </summary>
    internal sealed class JumpOnRetainedActionValidator : IRetainedActionValidator
    {
        /// <summary>
        /// Политика runtime-различий конкретного jump-on варианта.
        /// </summary>
        private readonly IJumpOnPolicy _policy;

        /// <summary>
        /// Finder для повторного runtime-подтверждения сохранённого action.
        /// </summary>
        private readonly JumpOnFireWindowFinder _fireWindowFinder;

        public JumpOnRetainedActionValidator(
            IJumpOnPolicy policy,
            JumpOnFireWindowFinder fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        /// <summary>
        /// Возвращает тип action, который валидирует этот экземпляр.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, можно ли оставить ранее выбранный jump-on action в текущем planning-состоянии.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет базовую совместимость context и action.
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            // Раскрывает context для дальнейших проверок.
            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            DecisionPoint decisionPoint = context.DecisionPoint;
            ObstacleSnapshot targetObstacle = context.TargetObstacle;
            PlannedAction action = context.Action;

            // Отсекает неполный context.
            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || targetObstacle == null
                || action == null)
            {
                return false;
            }

            // Восстанавливает актуальную jump-on chain, включая уже committed target за обычным gap.
            float targetChainHorizon = GetRetainedTargetChainHorizon(
                projectedWorldSnapshot,
                targetObstacle);
            if (!JumpOnTargetChainBuilder.TryBuildTargetChain(
                    planningState,
                    projectedWorldSnapshot,
                    decisionPoint.Chain,
                    targetChainHorizon,
                    out ObstacleChain actionChain))
                return false;

            // Получает runtime-дистанции действия.
            if (!_policy.TryGetTravel(out JumpOnTravel travel))
                return false;

            // Проверяет, что chain всё ещё ведёт к тому же target.
            if (!actionChain.TryFindFirstGroundJumpOnTarget(
                    planningState.Hamster.IsOnBottomLine,
                    out ObstacleSnapshot currentTargetObstacle,
                    out int currentTargetIndex,
                    out _)
                || currentTargetObstacle.InstanceId != targetObstacle.InstanceId)
            {
                return false;
            }

            // Вычисляет оставшийся fire shift.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
                return false;

            // Сохраненный trigger — representative-точка внутри окна, а не жесткий дедлайн.
            if (fireShift < 0f)
                fireShift = 0f;

            // Подтверждает outcome runtime resolver-ом.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            if (!_fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                travel,
                currentTargetIndex))
                return false;

            // Проверяет безопасность после полного завершения.
            return JumpOnPostActionSafety.IsSafeAfterCompletion(
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
            float projectionWorldShift,
            out float fireShift)
        {
            // Отсекает неполные данные.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Ищет live trigger obstacle.
            float projectedTriggerX = action.TriggerX - projectionWorldShift;
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
            }

            // Использует target как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }

        /// <summary>
        /// Возвращает правую границу поиска target-chain для уже выбранного retained action.
        /// </summary>
        private static float GetRetainedTargetChainHorizon(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle)
        {
            // Для новых candidates действует ScreenRightEdgeX, но retained target уже был выбран раньше.
            return targetObstacle.LeftX > projectedWorldSnapshot.ScreenRightEdgeX
                ? targetObstacle.LeftX
                : projectedWorldSnapshot.ScreenRightEdgeX;
        }
    }
}
