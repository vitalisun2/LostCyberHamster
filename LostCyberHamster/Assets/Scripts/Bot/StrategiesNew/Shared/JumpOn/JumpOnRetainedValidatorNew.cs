using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOn
{
    /// <summary>
    /// Проверяет retained JumpOn action для role-based planning path.
    /// </summary>
    internal sealed class JumpOnRetainedValidatorNew : IRetainedActionValidatorNew
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly IJumpOnPolicy _policy;
        private readonly JumpOnFireWindowFinderNew _fireWindowFinder;
        private readonly ObstacleChainBuilderNew _chainBuilder = new ObstacleChainBuilderNew();

        /// <summary>
        /// Создает role-based validator сохраненного JumpOn.
        /// </summary>
        public JumpOnRetainedValidatorNew(
            IJumpOnPolicy policy,
            JumpOnFireWindowFinderNew fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, что retained JumpOn все еще актуален и безопасен.
        /// </summary>
        public bool IsStillValid(RetainedActionContextNew context)
        {
            // Проверяет базовую совместимость context и action.
            if (context?.PlanningState?.Hamster == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || context.Action.TargetBottomLine.HasValue
                || context.Action.ResultRoofSupportInstanceId.HasValue)
            {
                return false;
            }

            // Проверяет текущую применимость retained target.
            PlannedAction action = context.Action;
            HamsterSnapshot hamster = context.PlanningState.Hamster;
            if (!CanStillExecute(hamster, context.RetainedObstacle, action))
                return false;

            // Пересобирает role-based chain и находит retained target внутри него.
            if (!TryBuildCurrentLaneChain(context, out ObstacleChainNew chain)
                || !TryFindRetainedTargetInChain(
                    chain,
                    context.RetainedObstacle,
                    out int targetObstacleIndex,
                    out int targetObstacleChainIndex))
            {
                return false;
            }

            // Получает runtime-дистанции действия.
            if (!_policy.TryGetTravel(out JumpOnTravel travel))
                return false;

            // Пересчитывает окно запуска для retained target.
            if (!JumpOnWindowCalculatorNew.TryCalculate(
                    hamster,
                    chain,
                    context.RetainedObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    travel,
                    out JumpOnWindowModel window))
            {
                return false;
            }

            // Восстанавливает оставшийся fire shift сохраненного action.
            if (!TryGetRemainingFireShift(
                    context.ProjectedWorldSnapshot,
                    context.RetainedObstacle,
                    action,
                    context.PlanningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            if (fireShift < 0f)
                fireShift = 0f;

            if (fireShift < window.FirstFireShift - ValidationEpsilon
                || fireShift > window.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            // Подтверждает runtime outcome и post-action safety.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(context.ProjectedWorldSnapshot);
            if (!_fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                    hamster,
                    baseObstacles,
                    fireShift,
                    travel,
                    targetObstacleIndex))
            {
                return false;
            }

            return TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                targetObstacleIndex,
                context.RetainedObstacle.InstanceId,
                fireShift + travel.ActionTravel);
        }

        /// <summary>
        /// Проверяет текущую применимость retained target для ground jump-on.
        /// </summary>
        private bool CanStillExecute(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            PlannedAction action)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.Run
                && !hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.Energy >= action.EnergyCost
                && targetObstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.CanJumpOnGroundObstacle(targetObstacle.ObstacleType);
        }

        /// <summary>
        /// Пересобирает role-based chain на текущей линии hamster.
        /// </summary>
        private bool TryBuildCurrentLaneChain(
            RetainedActionContextNew context,
            out ObstacleChainNew chain)
        {
            return _chainBuilder.TryBuild(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                context.PlanningState.NextObstacleIndex,
                out chain);
        }

        /// <summary>
        /// Находит retained target внутри актуальной role-based chain.
        /// </summary>
        private static bool TryFindRetainedTargetInChain(
            ObstacleChainNew chain,
            ObstacleSnapshot retainedTarget,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex)
        {
            // Сбрасывает результат и проверяет входы.
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;
            if (chain == null || retainedTarget == null)
                return false;

            // Ищет target с тем же instance id.
            for (int chainIndex = 0; chainIndex < chain.Count; chainIndex++)
            {
                ObstacleChainElementNew element = chain.Elements[chainIndex];
                if (element.Obstacle.InstanceId != retainedTarget.InstanceId)
                    continue;

                if (!element.HasRole(ObstacleRole.Target)
                    || !ObstacleClassifier.CanJumpOnGroundObstacle(element.Obstacle.ObstacleType))
                {
                    return false;
                }

                targetObstacleIndex = element.WorldIndex;
                targetObstacleChainIndex = chainIndex;
                return true;
            }

            return false;
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
            // Проверяет входные данные.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            // Сначала ищет trigger anchor по instance id.
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

            // Использует retained target как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
