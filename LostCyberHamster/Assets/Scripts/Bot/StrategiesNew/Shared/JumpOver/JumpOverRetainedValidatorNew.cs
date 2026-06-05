using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOver
{
    /// <summary>
    /// Проверяет retained JumpOver action для role-based planning path.
    /// </summary>
    internal sealed class JumpOverRetainedValidatorNew : IRetainedActionValidatorNew
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly IJumpOverPolicy _policy;
        private readonly JumpOverFireWindowFinderNew _fireWindowFinder;
        private readonly ObstacleChainBuilderNew _chainBuilder = new ObstacleChainBuilderNew();

        /// <summary>
        /// Создает role-based validator сохраненного JumpOver.
        /// </summary>
        public JumpOverRetainedValidatorNew(
            IJumpOverPolicy policy,
            JumpOverFireWindowFinderNew fireWindowFinder)
        {
            _policy = policy;
            _fireWindowFinder = fireWindowFinder;
        }

        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, что retained JumpOver все еще актуален и безопасен.
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

            // Проверяет, что retained obstacle все еще подходит для jump-over.
            PlannedAction action = context.Action;
            HamsterSnapshot hamster = context.PlanningState.Hamster;
            if (!CanStillExecute(hamster, context.RetainedObstacle, action))
                return false;

            // Пересобирает current-lane chain от текущего planning index.
            if (!TryBuildCurrentLaneChain(context, out ObstacleChainNew chain))
                return false;

            if (chain.First.Obstacle.InstanceId != context.RetainedObstacle.InstanceId)
                return false;

            // Пересчитывает fire window для сохраненного action.
            if (!JumpOverChainCalculatorNew.TryCalculate(
                    _policy,
                    hamster,
                    chain,
                    action.PostFireWorldShift,
                    out JumpOverChainModel chainWindow))
            {
                return false;
            }

            if (!TryGetRemainingFireShift(
                    context.ProjectedWorldSnapshot,
                    context.RetainedObstacle,
                    action,
                    context.PlanningState.ProjectionWorldShift,
                    out float fireShift))
            {
                return false;
            }

            if (fireShift < chainWindow.FirstFireShift - ValidationEpsilon
                || fireShift > chainWindow.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            // Подтверждает runtime outcome на текущем fire shift.
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                hamster,
                JumpObstacleProjection.BuildBase(context.ProjectedWorldSnapshot),
                fireShift,
                action.PostFireWorldShift,
                chainWindow);
        }

        /// <summary>
        /// Проверяет текущую применимость retained obstacle для jump-over.
        /// </summary>
        private bool CanStillExecute(
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            PlannedAction action)
        {
            return hamster != null
                && !hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.Energy >= action.EnergyCost
                && targetObstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.DamagesOnGroundContact(targetObstacle.ObstacleType)
                && _policy.CanJumpOverObstacle(targetObstacle.ObstacleType);
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
