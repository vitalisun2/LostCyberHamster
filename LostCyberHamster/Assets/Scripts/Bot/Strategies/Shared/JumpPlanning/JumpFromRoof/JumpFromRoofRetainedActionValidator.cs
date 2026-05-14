using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Проверяет, можно ли сохранить ранее выбранный прыжок с крыши на дорогу.
    /// </summary>
    internal sealed class JumpFromRoofRetainedActionValidator : IRetainedActionValidator
    {
        /// <summary>
        /// Допуск для проверки сохраненного fire shift относительно границ окна.
        /// </summary>
        private const float ValidationEpsilon = 0.0001f;

        /// <summary>
        /// Хранит runtime-отличия конкретного варианта прыжка с крыши.
        /// </summary>
        private readonly IJumpFromRoofPolicy _policy;

        /// <summary>
        /// Проверяет runtime outcome для сохраненного fire shift.
        /// </summary>
        private readonly JumpFromRoofFireWindowFinder _fireWindowFinder;

        public JumpFromRoofRetainedActionValidator(
            IJumpFromRoofPolicy policy,
            JumpFromRoofFireWindowFinder fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        /// <summary>
        /// Тип действия, который проверяет validator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, остается ли сохраненный action применимым к текущему planning context.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет action и kind.
            if (context == null || context.Action == null || context.Action.Kind != ActionKind)
                return false;

            // Извлекает retained context.
            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            DecisionPoint decisionPoint = context.DecisionPoint;
            ObstacleSnapshot targetObstacle = context.TargetObstacle;
            PlannedAction action = context.Action;

            // Проверяет обязательные данные.
            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null
                || targetObstacle == null)
            {
                return false;
            }

            // Проверяет, что target остался первым obstacle chain.
            if (decisionPoint.Chain.FirstObstacle.InstanceId != targetObstacle.InstanceId)
                return false;

            // Считывает актуальные runtime-дистанции.
            if (!_policy.TryGetTravel(out JumpFromRoofTravel travel))
                return false;

            // Пересчитывает актуальное fire window.
            if (!JumpFromRoofChainCalculator.TryCalculate(
                    planningState,
                    decisionPoint.Chain,
                    travel,
                    out JumpFromRoofChainModel chainModel))
            {
                return false;
            }

            // Восстанавливает текущий fire shift.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetObstacle,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            // Проверяет попадание fire shift в окно.
            if (fireShift < chainModel.FirstFireShift - ValidationEpsilon
                || fireShift > chainModel.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            // Подтверждает outcome через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState,
                baseObstacles,
                fireShift,
                travel);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift сохраненного action по trigger obstacle.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            // Проверяет вход.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Ищет trigger obstacle.
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

            // Использует target obstacle как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
