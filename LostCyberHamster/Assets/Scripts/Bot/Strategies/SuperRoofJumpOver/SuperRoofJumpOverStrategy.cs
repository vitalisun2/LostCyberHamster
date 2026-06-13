using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SuperRoofJumpOver
{
    /// <summary>
    /// Собирает role-based кандидаты super roof jump-over.
    /// </summary>
    internal sealed class SuperRoofJumpOverStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy super roof jump-over.
        /// </summary>
        private readonly IRoofJumpOverPolicy _policy;

        /// <summary>
        /// Specification применимости super roof jump-over.
        /// </summary>
        private readonly RoofJumpOverSpecification _specification;

        /// <summary>
        /// Resolver первого roof occupant hazard в role-based chain.
        /// </summary>
        private readonly RoofJumpOverActionResolver _actionResolver;

        /// <summary>
        /// Finder fire-window с runtime-проверкой результата.
        /// </summary>
        private readonly RoofJumpOverFireWindowFinder _fireWindowFinder;

        /// <summary>
        /// Simulator planning-перехода super roof jump-over.
        /// </summary>
        private readonly RoofJumpOverSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты super roof jump-over.
        /// </summary>
        public SuperRoofJumpOverStrategy()
        {
            _policy = new SuperRoofJumpOverPolicy();
            _specification = new RoofJumpOverSpecification(_policy);
            _actionResolver = new RoofJumpOverActionResolver();
            _fireWindowFinder = new RoofJumpOverFireWindowFinder(_policy);
            _simulator = new RoofJumpOverSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperRoofJumpOverExecutor(triggerGate);
            Simulator = _simulator;
        }

        /// <summary>
        /// Тип action, который создает стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor super roof jump-over.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator super roof jump-over.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет super-roof-jump-over action для hazard на текущем passive roof path.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            // Проверяет вход и выбирает role-based hazard.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)));

            if (!_actionResolver.TryResolve(
                    decisionPoint.Chain,
                    out ObstacleSnapshot hazardObstacle,
                    out _))
            {
                return PlanningStrategyResult.NotApplicable();
            }

            if (!_specification.IsSatisfiedBy(planningState, hazardObstacle))
                return PlanningStrategyResult.NotApplicable();

            // Получает travel и подтверждает fire-window через runtime resolver.
            if (!_policy.TryGetTravel(out RoofJumpOverTravel travel))
                return PlanningStrategyResult.NotApplicable();

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out RoofJumpOverChainModel chainModel,
                    out ObstacleSnapshot supportObstacle,
                    out float fireShift,
                    out string deadEndReason))
            {
                return DeadEnd(deadEndReason);
            }

            return PlanningStrategyResult.FromAction(BuildAction(
                _policy,
                planningState,
                chainModel,
                supportObstacle,
                fireShift,
                travel));
        }

        /// <summary>
        /// Создает dead-end результат для применимой super roof-jump-over strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(SuperRoofJumpOverStrategy), message);
        }

        /// <summary>
        /// Создает planned action для найденного super roof jump-over fire shift.
        /// </summary>
        private static PlannedAction BuildAction(
            IRoofJumpOverPolicy policy,
            PlanningState planningState,
            RoofJumpOverChainModel chainModel,
            ObstacleSnapshot supportObstacle,
            float fireShift,
            RoofJumpOverTravel travel)
        {
            // Считает trigger position по первому hazard.
            ObstacleSnapshot hazardObstacle = chainModel.FirstHazard;
            float projectedTriggerX = hazardObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                chainModel.FirstFireShift,
                chainModel.LastFireShift);

            // Возвращает action с сохранением result roof support.
            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + travel.RoofJumpTravel,
                postFireWorldShift: travel.RoofJumpTravel,
                chainModel.LastHazardIndex,
                targetObstacleInstanceId: hazardObstacle.InstanceId,
                triggerObstacleInstanceId: hazardObstacle.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: BuildDescription(policy, chainModel),
                resultRoofSupportInstanceId: supportObstacle.InstanceId,
                triggerWindow: triggerWindow);
        }

        /// <summary>
        /// Формирует описание planned action.
        /// </summary>
        private static string BuildDescription(
            IRoofJumpOverPolicy policy,
            RoofJumpOverChainModel chainModel)
        {
            string baseDescription = $"{policy.DescriptionPrefix} {chainModel.FirstHazard.ObstacleType}";
            return chainModel.HazardCount <= 1
                ? baseDescription
                : $"{baseDescription} x{chainModel.HazardCount}";
        }
    }
}
