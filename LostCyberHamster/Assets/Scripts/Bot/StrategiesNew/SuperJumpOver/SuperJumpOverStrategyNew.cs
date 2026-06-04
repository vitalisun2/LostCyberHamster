using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.StrategiesNew.Shared.JumpOver;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.StrategiesNew.SuperJumpOver
{
    /// <summary>
    /// Собирает role-based кандидаты ground super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverStrategyNew : IPlanningStrategyNew
    {
        private readonly IJumpOverPolicy _policy;
        private readonly IBotStrategySpecification _specification;
        private readonly JumpOverFireWindowFinderNew _fireWindowFinder;
        private readonly JumpOverSimulator _simulator;

        /// <summary>
        /// Создает strategy и runtime-компоненты super jump-over.
        /// </summary>
        public SuperJumpOverStrategyNew()
        {
            _policy = new SuperJumpOverPolicy();
            _specification = new JumpOverSpecificationNew(_policy);
            _fireWindowFinder = new JumpOverFireWindowFinderNew(_policy);
            _simulator = new JumpOverSimulator(_policy);
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            Executor = new SuperJumpOverExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new JumpOverRetainedValidatorNew(_policy, _fireWindowFinder);
        }

        public BotActionKind ActionKind => _policy.ActionKind;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }
        public IRetainedActionValidatorNew RetainedValidator { get; }

        /// <summary>
        /// Добавляет super jump-over action, если первый role-based obstacle безопасно перепрыгивается.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPointNew decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет обязательные аргументы.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Выбирает blocking threat из текущей role-based ситуации.
            if (!TryResolveBlockingThreat(
                    decisionPoint,
                    out ObstacleSnapshot blockingThreat,
                    out int blockingThreatIndex))
            {
                return;
            }

            // Проверяет применимость super-policy к выбранной blocking threat.
            if (!_specification.IsSatisfiedBy(planningState, blockingThreat))
            {
                return;
            }

            // Получает runtime travel и подтверждает fire window.
            if (!_policy.TryGetTravel(out float superJumpTravel))
                return;

            if (!_fireWindowFinder.TryFindFireShift(
                    planningState,
                    worldSnapshot,
                    decisionPoint.Chain,
                    superJumpTravel,
                    out JumpOverChainModel chainWindow,
                    out float fireShift))
            {
                return;
            }

            // Добавляет safe action в общий набор кандидатов без сравнения с обычным JumpOver.
            actions.Add(BuildAction(
                _policy,
                planningState,
                blockingThreat,
                blockingThreatIndex,
                chainWindow,
                fireShift,
                superJumpTravel));
        }

        /// <summary>
        /// Пытается выбрать первую blocking threat текущей focus-ситуации.
        /// </summary>
        private static bool TryResolveBlockingThreat(
            DecisionPointNew decisionPoint,
            out ObstacleSnapshot blockingThreat,
            out int blockingThreatIndex)
        {
            blockingThreat = null;
            blockingThreatIndex = -1;

            if (decisionPoint?.Chain == null)
                return false;

            ObstacleChainElementNew firstElement = decisionPoint.Chain.First;
            if (!firstElement.HasRole(ObstacleRole.BlockingThreat))
                return false;

            blockingThreat = firstElement.Obstacle;
            blockingThreatIndex = firstElement.WorldIndex;
            return true;
        }

        /// <summary>
        /// Создает planned action для подтвержденного super jump-over fire shift.
        /// </summary>
        private static PlannedAction BuildAction(
            IJumpOverPolicy policy,
            PlanningState planningState,
            ObstacleSnapshot blockingThreat,
            int blockingThreatIndex,
            JumpOverChainModel chainWindow,
            float fireShift,
            float superJumpTravel)
        {
            // Рассчитывает trigger window относительно blocking threat.
            float projectedTriggerX = blockingThreat.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                chainWindow.FirstFireShift,
                chainWindow.LastFireShift);

            // Возвращает super jump-over action без objective/branch ranking.
            return new PlannedAction(
                policy.ActionKind,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + superJumpTravel,
                postFireWorldShift: superJumpTravel,
                blockingThreatIndex,
                targetObstacleInstanceId: blockingThreat.InstanceId,
                targetBottomLine: null,
                energyCost: policy.EnergyCost,
                description: $"{policy.DescriptionPrefix} {blockingThreat.ObstacleType}",
                triggerWindow: triggerWindow);
        }
    }
}
