using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.StrategiesNew.SwitchLane
{
    /// <summary>
    /// Собирает role-based кандидаты SwitchLane для нового planning path.
    /// </summary>
    internal sealed class SwitchLaneStrategyNew : IPlanningStrategyNew
    {
        private readonly SwitchLaneSpecificationNew _specification;
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;
        private readonly SwitchLaneSimulator _simulator;

        public SwitchLaneStrategyNew()
        {
            // Создаёт зависимости стратегии.
            _specification = new SwitchLaneSpecificationNew();
            _fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _simulator = new SwitchLaneSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует runtime-компоненты, которые не зависят от старого DecisionPoint.
            Executor = new SwitchLaneExecutor(triggerGate);
            Simulator = _simulator;
            RetainedValidator = new SwitchLaneRetainedValidatorNew(_fireWindowCalculator);
        }

        public BotActionKind ActionKind => BotActionKind.SwitchLane;
        public IActionExecutionHandler Executor { get; }
        public ISimulator Simulator { get; }
        public IRetainedActionValidatorNew RetainedValidator { get; }

        /// <summary>
        /// Собирает допустимые действия смены линии для role-based точки принятия решения.
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

            // Отбирает obstacle, для которого допустима смена линии.
            if (!_specification.TryFindBlockingThreat(
                    planningState,
                    decisionPoint,
                    out ObstacleSnapshot threatObstacle,
                    out int threatObstacleIndex))
            {
                return;
            }

            // Вычисляет целевую линию и верхнюю границу окна запуска.
            HamsterSnapshot hamster = planningState.Hamster;
            bool targetBottomLine = !hamster.IsOnBottomLine;
            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    hamster,
                    threatObstacle,
                    out float latestFireShift))
            {
                return;
            }

            // Строит все варианты action в безопасных окнах.
            IReadOnlyList<float> selectionRatios = GetSelectionRatios(planningState);
            IReadOnlyList<SwitchLaneFireWindowSample> fireWindowSamples =
                _fireWindowCalculator.CollectFireWindowSamples(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    selectionRatios);

            for (int sampleIndex = 0; sampleIndex < fireWindowSamples.Count; sampleIndex++)
            {
                SwitchLaneFireWindowSample fireWindowSample = fireWindowSamples[sampleIndex];
                actions.Add(BuildAction(
                    planningState,
                    threatObstacle,
                    threatObstacleIndex,
                    targetBottomLine,
                    fireWindowSample));
            }
        }

        /// <summary>
        /// Создаёт запланированное действие смены линии для выбранного момента запуска.
        /// </summary>
        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot threatObstacle,
            int threatObstacleIndex,
            bool targetBottomLine,
            SwitchLaneFireWindowSample fireWindowSample)
        {
            // Рассчитывает мировую точку срабатывания действия.
            float fireShift = fireWindowSample.FireShift;
            float projectedTriggerX = threatObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            ActionTriggerWindow triggerWindow = ActionTriggerWindow.FromSelectedTrigger(
                triggerX,
                fireShift,
                fireWindowSample.FirstFireShift,
                fireWindowSample.LastFireShift);

            // Создаёт action с рассчитанными параметрами исполнения.
            return new PlannedAction(
                BotActionKind.SwitchLane,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + SwitchLaneTiming.PostFirePlanningTravel,
                postFireWorldShift: SwitchLaneTiming.PostFirePlanningTravel,
                threatObstacleIndex,
                targetObstacleInstanceId: threatObstacle.InstanceId,
                targetBottomLine: targetBottomLine,
                energyCost: 0,
                description: $"Switch lane before {threatObstacle.ObstacleType}",
                triggerWindow: triggerWindow);
        }

        /// <summary>
        /// Возвращает ratio safe-window для role-based SwitchLane.
        /// </summary>
        private static IReadOnlyList<float> GetSelectionRatios(PlanningState planningState)
        {
            // Добавляет ранний вариант при достаточной энергии для потенциального target-hunt.
            HamsterSnapshot hamster = planningState?.Hamster;
            if (JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster))
                return new[]
                {
                    SwitchLaneTiming.EarlyWindowSelectionRatio,
                    SwitchLaneTiming.MidWindowSelectionRatio
                };

            // Возвращает обычный вариант уклонения.
            return new[]
            {
                SwitchLaneTiming.MidWindowSelectionRatio
            };
        }
    }
}
