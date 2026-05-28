using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Common;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Собирает компоненты SwitchLane strategy.
    /// </summary>
    internal sealed class SwitchLaneStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Минимальная энергия, при которой SwitchLane добавляет ранний вариант для потенциального JumpOn.
        /// </summary>
        private const int _highPriorityJumpOnEnergyThreshold = 40;

        private readonly SwitchLaneSpecification _specification;
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;
        private readonly SwitchLaneSimulator _simulator;

        public SwitchLaneStrategy()
        {
            // Создаёт внутренние зависимости стратегии.
            _specification = new SwitchLaneSpecification();
            _fireWindowCalculator = new SwitchLaneFireWindowCalculator();
            _simulator = new SwitchLaneSimulator();
            var triggerGate = new ActionTriggerGate(new LiveObstacleResolver());

            // Публикует runtime-компоненты стратегии.
            Executor = new SwitchLaneExecutor(triggerGate);
            RetainedValidator = new SwitchLaneRetainedValidator(_specification, _fireWindowCalculator);
            Simulator = _simulator;
        }

        public BotActionKind ActionKind => BotActionKind.SwitchLane;
        public IActionExecutionHandler Executor { get; }
        public IRetainedActionValidator RetainedValidator { get; }
        public ISimulator Simulator { get; }

        /// <summary>
        /// Собирает допустимые действия смены линии для текущей точки принятия решения.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет обязательные аргументы.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (worldSnapshot, nameof(worldSnapshot)),
                (decisionPoint, nameof(decisionPoint)),
                (actions, nameof(actions)));

            // Отбирает obstacle, для которого допустима смена линии.
            if (!_specification.IsSatisfiedBy(planningState, decisionPoint, out ObstacleSnapshot targetObstacle, out int targetObstacleIndex))
                return;

            // Вычисляет линию и доступное окно запуска.
            HamsterSnapshot hamster = planningState.Hamster;
            bool targetBottomLine = !hamster.IsOnBottomLine;
            if (!_fireWindowCalculator.TryGetLatestFireShift(hamster, targetObstacle, out float latestFireShift))
                return;

            if (decisionPoint.HasFireBeforeObstacle
                && !TryClampLatestFireShiftBeforeDeadline(
                    hamster,
                    decisionPoint.FireBeforeObstacle,
                    ref latestFireShift))
            {
                return;
            }

            // Строит все варианты action в найденном окне.
            IReadOnlyList<float> selectionRatios = GetSelectionRatios(
                planningState,
                decisionPoint);
            IReadOnlyList<float> fireShifts = _fireWindowCalculator.CollectFireShifts(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift,
                selectionRatios);

            for (int fireShiftIndex = 0; fireShiftIndex < fireShifts.Count; fireShiftIndex++)
                actions.Add(BuildAction(planningState, targetObstacle, targetObstacleIndex, targetBottomLine, fireShifts[fireShiftIndex]));
        }

        /// <summary>
        /// Создаёт запланированное действие смены линии для выбранного момента запуска.
        /// </summary>
        private static PlannedAction BuildAction(
            PlanningState planningState,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            bool targetBottomLine,
            float fireShift)
        {
            // Рассчитывает мировую точку срабатывания действия.
            float projectedTriggerX = targetObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;

            // Создаёт action с рассчитанными параметрами исполнения.
            return new PlannedAction(
                BotActionKind.SwitchLane,
                triggerX,
                renderWorldX: triggerX,
                completionWorldShift: fireShift + SwitchLaneTiming.PostFirePlanningTravel,
                postFireWorldShift: SwitchLaneTiming.PostFirePlanningTravel,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacle.InstanceId,
                targetBottomLine: targetBottomLine,
                energyCost: 0,
                description: $"Switch lane before {targetObstacle.ObstacleType}");
        }

        /// <summary>
        /// Возвращает ratio safe-window, которые нужно попробовать для текущей точки решения.
        /// </summary>
        private static IReadOnlyList<float> GetSelectionRatios(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            // Выбирает ранний запуск для явной JumpOn-возможности.
            if (decisionPoint != null && decisionPoint.IsJumpOnOpportunity)
                return new[]
                {
                    SwitchLaneTiming.EarlyWindowSelectionRatio
                };

            // Добавляет ранний вариант при достаточной энергии для потенциального JumpOn.
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster != null && hamster.Energy >= _highPriorityJumpOnEnergyThreshold)
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

        private bool TryClampLatestFireShiftBeforeDeadline(
            HamsterSnapshot hamster,
            ObstacleSnapshot deadlineObstacle,
            ref float latestFireShift)
        {
            if (deadlineObstacle == null)
                return latestFireShift > 0f;

            if (!_fireWindowCalculator.TryGetLatestFireShift(hamster, deadlineObstacle, out float deadlineLatestFireShift))
                return false;

            if (deadlineLatestFireShift < latestFireShift)
                latestFireShift = deadlineLatestFireShift;

            return latestFireShift > 0f;
        }
    }
}
