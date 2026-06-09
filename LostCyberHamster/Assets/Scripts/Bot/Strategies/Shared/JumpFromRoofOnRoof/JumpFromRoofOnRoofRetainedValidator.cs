using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Проверяет сохраненные roof-to-roof actions для role-based planning path.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofRetainedValidator : IRetainedActionValidator
    {
        /// <summary>
        /// Допуск при сравнении сохраненного fire shift с пересчитанным fire-window.
        /// </summary>
        private const float ValidationEpsilon = 0.0001f;

        /// <summary>
        /// Policy конкретного варианта roof-to-roof прыжка.
        /// </summary>
        private readonly IJumpFromRoofOnRoofPolicy _policy;

        /// <summary>
        /// Finder для повторной проверки target roof и runtime outcome.
        /// </summary>
        private readonly JumpFromRoofOnRoofFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Specification применимости сохраненного roof-to-roof action.
        /// </summary>
        private readonly JumpFromRoofOnRoofSpecification _specification;

        /// <summary>
        /// Builder для восстановления актуальной blocker chain.
        /// </summary>
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();

        public JumpFromRoofOnRoofRetainedValidator(
            IJumpFromRoofOnRoofPolicy policy,
            JumpFromRoofOnRoofFireWindowFinder fireWindowFinder,
            JumpFromRoofOnRoofSpecification specification)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
            _specification = specification;
        }

        /// <summary>
        /// Тип action, который проверяет validator.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Возвращает true, если сохраненный action всё ещё применим и ведет к той же target roof.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет retained context и action contract.
            if (context?.PlanningState?.Hamster == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind)
            {
                return false;
            }

            PlanningState planningState = context.PlanningState;
            WorldSnapshot projectedWorldSnapshot = context.ProjectedWorldSnapshot;
            ObstacleSnapshot retainedTargetRoof = context.RetainedObstacle;
            PlannedAction action = context.Action;

            // Проверяет применимость и runtime-дистанции.
            if (!_specification.IsSatisfiedBy(planningState))
                return false;

            if (!_policy.TryGetTravel(out JumpFromRoofOnRoofTravel travel))
                return false;

            // Восстанавливает актуальную blocker chain для roof-to-roof сценария.
            if (!TryBuildBlockerChain(planningState, projectedWorldSnapshot, out ObstacleChain chain))
                return false;

            // Заново находит target roof для текущего roof-to-roof сценария.
            if (!_fireWindowFinder.TryFindTargetRoof(
                    planningState,
                    projectedWorldSnapshot,
                    chain,
                    travel,
                    out ObstacleSnapshot lastRoof,
                    out ObstacleSnapshot runFromRoofBlocker,
                    out ObstacleSnapshot lastObstacleBeforeTargetRoof,
                    out ObstacleSnapshot targetRoof,
                    out int targetRoofIndex))
            {
                return false;
            }

            // Сверяет сохраненный action с актуальной target roof.
            if (targetRoof.InstanceId != retainedTargetRoof.InstanceId)
                return false;

            if (action.TargetObstacleInstanceId != targetRoof.InstanceId)
                return false;

            if (action.TargetObstacleIndex != targetRoofIndex)
                return false;

            if (action.ResultRoofSupportInstanceId != targetRoof.InstanceId)
                return false;

            // Пересчитывает актуальное fire-window.
            if (!JumpFromRoofOnRoofWindowCalculator.TryCalculate(
                    planningState,
                    lastRoof,
                    targetRoof,
                    runFromRoofBlocker,
                    lastObstacleBeforeTargetRoof,
                    _policy.BigAliveCollisionPaddingRatio,
                    travel,
                    out float firstFireShift,
                    out float lastFireShift,
                    out _))
            {
                return false;
            }

            // Восстанавливает текущий fire shift сохраненного action.
            if (!TryGetRemainingFireShift(
                    projectedWorldSnapshot,
                    targetRoof,
                    action,
                    planningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            // Проверяет попадание fire shift в окно.
            if (fireShift < firstFireShift - ValidationEpsilon
                || fireShift > lastFireShift + ValidationEpsilon)
            {
                return false;
            }

            // Подтверждает outcome через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                targetRoof.InstanceId,
                fireShift,
                travel);
        }

        /// <summary>
        /// Строит role-based chain от первого obstacle после текущей passive roof chain.
        /// </summary>
        private bool TryBuildBlockerChain(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            out ObstacleChain chain)
        {
            // Определяет старт после текущей passive roof chain.
            chain = null;
            int firstDetectionIndex = planningState.NextObstacleIndex;
            if (RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out _,
                    out int lastRoofIndex))
            {
                firstDetectionIndex = lastRoofIndex + 1;
            }

            // Строит chain для текущей линии хомяка.
            return _chainBuilder.TryBuild(
                planningState,
                projectedWorldSnapshot,
                firstDetectionIndex,
                out chain);
        }

        /// <summary>
        /// Восстанавливает оставшийся fire shift сохраненного action по target roof.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetRoof,
            PlannedAction action,
            float projectionWorldShift,
            out float fireShift)
        {
            // Проверяет обязательный context.
            if (projectedWorldSnapshot == null || targetRoof == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Пытается восстановить fire shift по сохраненному trigger obstacle.
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

            // Использует target roof как fallback.
            fireShift = targetRoof.LeftX - projectedTriggerX;
            return true;
        }
    }
}
