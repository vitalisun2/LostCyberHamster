using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Проверяет сохраненные roof-jump-over actions для role-based planning path.
    /// </summary>
    internal sealed class RoofJumpOverRetainedValidator : IRetainedActionValidator
    {
        /// <summary>
        /// Допуск при сравнении сохраненного fire shift с пересчитанным fire-window.
        /// </summary>
        private const float ValidationEpsilon = 0.0001f;

        /// <summary>
        /// Policy конкретного варианта roof jump-over.
        /// </summary>
        private readonly IRoofJumpOverPolicy _policy;

        /// <summary>
        /// Finder для повторной проверки runtime outcome сохраненного action.
        /// </summary>
        private readonly RoofJumpOverFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Specification применимости сохраненного roof jump-over.
        /// </summary>
        private readonly RoofJumpOverSpecification _specification;

        /// <summary>
        /// Builder для восстановления актуальной roof-hazard chain.
        /// </summary>
        private readonly ObstacleChainBuilder _chainBuilder = new ObstacleChainBuilder();

        /// <summary>
        /// Создает validator для сохраненных roof-jump-over actions.
        /// </summary>
        public RoofJumpOverRetainedValidator(
            IRoofJumpOverPolicy policy,
            RoofJumpOverFireWindowFinder fireWindowFinder,
            RoofJumpOverSpecification specification)
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
        /// Возвращает true, если сохраненный action всё ещё применим, находится в окне и ведет к той же roof support.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            // Проверяет retained context и наличие expected support.
            if (context?.PlanningState?.Hamster == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || context.Action.TargetBottomLine.HasValue
                || !context.Action.ResultRoofSupportInstanceId.HasValue)
            {
                return false;
            }

            // Считывает travel и перестраивает текущую roof-hazard chain.
            PlannedAction action = context.Action;
            if (!_policy.TryGetTravel(out RoofJumpOverTravel travel)
                || !TryBuildRoofHazardChain(context, out ObstacleChain sourceChain))
            {
                return false;
            }

            if (sourceChain.FirstObstacle.InstanceId != context.RetainedObstacle.InstanceId)
                return false;

            if (!_specification.IsSatisfiedBy(context.PlanningState, context.RetainedObstacle))
                return false;

            // Пересчитывает fire-window для актуальной chain.
            if (!RoofJumpOverChainCalculator.TryCalculate(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    sourceChain,
                    travel,
                    out RoofJumpOverChainModel chainModel))
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

            // Проверяет окно и runtime outcome с тем же support.
            if (fireShift < chainModel.FirstFireShift - ValidationEpsilon
                || fireShift > chainModel.LastFireShift + ValidationEpsilon)
            {
                return false;
            }

            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(context.ProjectedWorldSnapshot);
            return _fireWindowFinder.CheckRuntimeOutcomeAtFireShift(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                baseObstacles,
                action.ResultRoofSupportInstanceId.Value,
                fireShift,
                travel);
        }

        /// <summary>
        /// Перестраивает chain от первого occupant hazard на текущем passive roof path.
        /// </summary>
        private bool TryBuildRoofHazardChain(
            RetainedActionContext context,
            out ObstacleChain chain)
        {
            // Находит актуальный первый hazard на passive roof path.
            chain = null;
            if (!TryFindFirstRoofOccupantHazardIndex(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    out int firstHazardIndex))
            {
                return false;
            }

            // Строит role-based chain от найденного hazard.
            return _chainBuilder.TryBuild(
                context.PlanningState,
                context.ProjectedWorldSnapshot,
                firstHazardIndex,
                out chain);
        }

        /// <summary>
        /// Находит первый damaging occupant на текущем passive roof path.
        /// </summary>
        private static bool TryFindFirstRoofOccupantHazardIndex(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            out int firstHazardIndex)
        {
            // Проверяет наличие projected obstacles.
            firstHazardIndex = -1;
            if (projectedWorldSnapshot?.Obstacles == null)
                return false;

            // Сканирует snapshot до первого damaging occupant.
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (!RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                        planningState,
                        projectedWorldSnapshot,
                        obstacle,
                        out _,
                        out _))
                {
                    continue;
                }

                firstHazardIndex = obstacleIndex;
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
            // Проверяет обязательный context.
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
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

            // Использует target obstacle как fallback.
            fireShift = targetObstacle.LeftX - projectedTriggerX;
            return true;
        }
    }
}
