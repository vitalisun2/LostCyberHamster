using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Собирает role-based нулевой transition ожидания безопасного пассивного схода с крыши.
    /// </summary>
    internal sealed class PassiveRoofExitStrategy : IPlanningStrategy
    {
        /// <summary>
        /// Policy passive roof exit action.
        /// </summary>
        private readonly PassiveRoofExitPolicy _policy;

        public PassiveRoofExitStrategy()
        {
            // Инициализирует runtime и planning компоненты.
            _policy = new PassiveRoofExitPolicy();
            Executor = new PassiveRoofExitExecutor(_policy);
            Simulator = new PassiveRoofExitSimulator(_policy);
        }

        /// <summary>
        /// Возвращает тип действия passive roof exit.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Runtime executor passive roof exit.
        /// </summary>
        public IActionExecutionHandler Executor { get; }

        /// <summary>
        /// Simulator passive roof exit.
        /// </summary>
        public ISimulator Simulator { get; }

        /// <summary>
        /// Быстро проверяет roof-run context для пассивного схода с крыши.
        /// </summary>
        public bool CanConsider(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return PlanningStrategyApplicability.IsRoofRunCurrentLane(planningState, decisionPoint)
                || (PlanningStrategyApplicability.IsMovingBoundary(
                        planningState,
                        decisionPoint,
                        MovingBoundaryKind.PassiveRoofExit)
                    && PlanningStrategyApplicability.CanPlanRoofRun(planningState.Hamster));
        }

        /// <summary>
        /// Добавляет no-input passive roof exit action, если естественный сход с крыши безопасен.
        /// </summary>
        public PlanningStrategyResult CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint)
        {
            // Проверяет входные данные.
            if (planningState == null || worldSnapshot == null || decisionPoint == null)
                return PlanningStrategyResult.NotApplicable();

            // Получает runtime travel автоматического схода.
            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return PlanningStrategyResult.NotApplicable();

            // Строит safe transition model по типу planning-ситуации.
            PassiveRoofExitModel model;
            string deadEndReason;
            bool hasModel;
            if (decisionPoint.Kind == DecisionPointKind.MovingBoundary)
            {
                hasModel = PassiveRoofExitPlanner.TryBuildMovingBoundaryModel(
                    planningState,
                    worldSnapshot,
                    runFromRoofTravel,
                    out model,
                    out deadEndReason);
            }
            else
            {
                hasModel = PassiveRoofExitPlanner.TryBuildModel(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    runFromRoofTravel,
                    out model,
                    out deadEndReason);
            }

            if (!hasModel)
            {
                return string.IsNullOrEmpty(deadEndReason)
                    ? PlanningStrategyResult.NotApplicable()
                    : DeadEnd(deadEndReason);
            }

            // Добавляет no-input planned action.
            return PlanningStrategyResult.FromAction(BuildAction(model));
        }

        /// <summary>
        /// Создает dead-end результат для применимой passive roof exit strategy.
        /// </summary>
        private static PlanningStrategyResult DeadEnd(string message)
        {
            return PlanningStrategyResult.DeadEnd(nameof(PassiveRoofExitStrategy), message);
        }

        /// <summary>
        /// Создает planned action для safe passive roof exit transition.
        /// </summary>
        private PlannedAction BuildAction(PassiveRoofExitModel model)
        {
            // Выбирает stable trigger anchor на последней roof.
            float triggerX = model.LastRoof.LeftX;
            string description = model.HasContextObstacle
                ? $"{_policy.DescriptionPrefix} before {model.ContextObstacle.ObstacleType}"
                : $"{_policy.DescriptionPrefix} transition";
            int targetObstacleIndex = model.HasContextObstacle
                ? model.ContextObstacleIndex
                : -1;
            int? targetObstacleInstanceId = model.HasContextObstacle
                ? (int?)model.ContextObstacle.InstanceId
                : null;

            // Возвращает zero-cost no-tap action.
            return new PlannedAction(
                _policy.ActionKind,
                triggerX,
                triggerX,
                model.CompletionWorldShift,
                model.CompletionWorldShift,
                targetObstacleIndex,
                targetObstacleInstanceId: targetObstacleInstanceId,
                triggerObstacleInstanceId: model.LastRoof.InstanceId,
                energyCost: 0,
                description: description);
        }
    }
}
