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

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Собирает role-based кандидаты обычного roof jump-over.
    /// </summary>
    internal sealed class RoofJumpOverStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy обычного roof jump-over.
        /// </summary>
        private readonly IRoofJumpOverPolicy _policy;

        /// <summary>
        /// Specification применимости обычного roof jump-over.
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
        /// Simulator planning-перехода обычного roof jump-over.
        /// </summary>
        private readonly RoofJumpOverSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты обычного roof jump-over.
        /// </summary>
        public RoofJumpOverStrategy()
        {
            _policy = new RoofJumpOverPolicy();
            _specification = new RoofJumpOverSpecification(_policy);
            _actionResolver = new RoofJumpOverActionResolver();
            _fireWindowFinder = new RoofJumpOverFireWindowFinder(_policy);
            _simulator = new RoofJumpOverSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new RoofJumpOverExecutor(triggerGate);
            Simulator = _simulator;
        }

        /// <summary>
        /// Тип action, который создает стратегия.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor обычного roof jump-over.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator обычного roof jump-over.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Добавляет roof-jump-over action для hazard на текущем passive roof path.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет вход и выбирает role-based hazard.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            if (!_actionResolver.TryResolve(
                    decisionPoint.Chain,
                    out ObstacleSnapshot hazardObstacle,
                    out _))
            {
                return;
            }

            if (!_specification.IsSatisfiedBy(planningState, hazardObstacle))
                return;

            // Получает travel и подтверждает fire-window через runtime resolver.
            if (!_policy.TryGetTravel(out RoofJumpOverTravel travel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    travel,
                    out RoofJumpOverChainModel chainModel,
                    out ObstacleSnapshot supportObstacle,
                    out float fireShift))
            {
                return;
            }

            actions.Add(BuildAction(
                _policy,
                planningState,
                chainModel,
                supportObstacle,
                fireShift,
                travel));
        }

        /// <summary>
        /// Создает planned action для найденного roof jump-over fire shift.
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
