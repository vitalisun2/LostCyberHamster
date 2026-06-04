using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;

namespace Assets.Scripts.Bot.Planning.RetainedValidation
{
    /// <summary>
    /// Диспетчеризует role-based проверку committed action без восстановления decision point.
    /// </summary>
    public sealed class RetainedActionRevalidatorNew
    {
        private readonly IReadOnlyDictionary<BotActionKind, IRetainedActionValidatorNew> _validatorsByKind;

        /// <summary>
        /// Создает role-based revalidator поверх validators новых стратегий.
        /// </summary>
        internal RetainedActionRevalidatorNew(IReadOnlyList<IPlanningStrategyNew> strategies)
        {
            var validatorsByKind = new Dictionary<BotActionKind, IRetainedActionValidatorNew>();
            if (strategies == null)
            {
                _validatorsByKind = validatorsByKind;
                return;
            }

            for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
            {
                IPlanningStrategyNew strategy = strategies[strategyIndex];
                if (strategy?.RetainedValidator == null)
                    continue;

                if (validatorsByKind.ContainsKey(strategy.ActionKind))
                {
                    throw new InvalidOperationException(
                        $"Для role-based strategy зарегистрировано больше одного retained validator: kind={strategy.ActionKind}");
                }

                validatorsByKind.Add(strategy.ActionKind, strategy.RetainedValidator);
            }

            _validatorsByKind = validatorsByKind;
        }

        /// <summary>
        /// Возвращает true, если retained-action все еще безопасен и актуален.
        /// </summary>
        public bool IsStillValid(PlanningState planningState, PlannedAction action, WorldSnapshot worldSnapshot)
        {
            // Проверяет входы и проецирует snapshot к retained planning state.
            if (planningState == null || action == null || worldSnapshot == null)
                return false;

            WorldSnapshot projectedWorldSnapshot = PlanningSnapshotProjector.Project(worldSnapshot, planningState);
            if (projectedWorldSnapshot == null)
                return false;

            // Находит obstacle, к которому привязан retained action.
            if (!TryFindRetainedObstacle(
                    projectedWorldSnapshot,
                    action,
                    out ObstacleSnapshot retainedObstacle,
                    out int retainedObstacleIndex))
            {
                return false;
            }

            // Передает смысловую проверку validator'у конкретной strategy.
            if (!_validatorsByKind.TryGetValue(action.Kind, out IRetainedActionValidatorNew validator))
                return false;

            return validator.IsStillValid(new RetainedActionContextNew(
                planningState,
                projectedWorldSnapshot,
                retainedObstacle,
                retainedObstacleIndex,
                action));
        }

        /// <summary>
        /// Находит obstacle retained-action по instance id или fallback-индексу.
        /// </summary>
        private static bool TryFindRetainedObstacle(
            WorldSnapshot projectedWorldSnapshot,
            PlannedAction action,
            out ObstacleSnapshot retainedObstacle,
            out int retainedObstacleIndex)
        {
            // Сбрасывает результат и проверяет входы.
            retainedObstacle = null;
            retainedObstacleIndex = -1;
            if (projectedWorldSnapshot?.Obstacles == null || action == null)
                return false;

            // Сначала использует стабильный instance id.
            if (action.TargetObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != action.TargetObstacleInstanceId.Value)
                        continue;

                    retainedObstacle = obstacle;
                    retainedObstacleIndex = obstacleIndex;
                    return true;
                }
            }

            // Fallback по индексу нужен только для actions без instance id.
            if (action.TargetObstacleIndex < 0 || action.TargetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count)
                return false;

            retainedObstacleIndex = action.TargetObstacleIndex;
            retainedObstacle = projectedWorldSnapshot.Obstacles[retainedObstacleIndex];
            return retainedObstacle != null;
        }
    }
}
