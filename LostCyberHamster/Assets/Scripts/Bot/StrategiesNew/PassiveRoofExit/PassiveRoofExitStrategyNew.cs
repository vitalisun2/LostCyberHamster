using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;

namespace Assets.Scripts.Bot.StrategiesNew.PassiveRoofExit
{
    /// <summary>
    /// Собирает role-based нулевой transition ожидания безопасного пассивного схода с крыши.
    /// </summary>
    internal sealed class PassiveRoofExitStrategyNew : IPlanningStrategyNew
    {
        /// <summary>
        /// Policy passive roof exit action.
        /// </summary>
        private readonly PassiveRoofExitPolicy _policy;

        public PassiveRoofExitStrategyNew()
        {
            // Инициализирует runtime и planning компоненты.
            _policy = new PassiveRoofExitPolicy();
            Executor = new PassiveRoofExitExecutor(_policy);
            Simulator = new PassiveRoofExitSimulator(_policy);
            RetainedValidator = new PassiveRoofExitRetainedValidatorNew(_policy);
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
        /// Validator сохраненных passive roof exit actions.
        /// </summary>
        public IRetainedActionValidatorNew RetainedValidator { get; }

        /// <summary>
        /// Добавляет no-input passive roof exit action, если естественный сход с крыши безопасен.
        /// </summary>
        public void CollectActions(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPointNew decisionPoint,
            List<PlannedAction> actions)
        {
            // Проверяет входные данные.
            if (actions == null)
                return;

            // Получает runtime travel автоматического схода.
            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return;

            // Строит safe transition model.
            if (!PassiveRoofExitPlannerNew.TryBuildModel(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    runFromRoofTravel,
                    out PassiveRoofExitModel model))
            {
                return;
            }

            // Добавляет no-input planned action.
            actions.Add(BuildAction(planningState, model));
        }

        /// <summary>
        /// Создает planned action для safe passive roof exit transition.
        /// </summary>
        private PlannedAction BuildAction(
            PlanningState planningState,
            PassiveRoofExitModel model)
        {
            // Выбирает stable trigger anchor на последней roof.
            float triggerX = model.LastRoof.LeftX + planningState.ProjectionWorldShift;
            string description = $"{_policy.DescriptionPrefix} before {model.ContextObstacle.ObstacleType}";

            // Возвращает zero-cost no-tap action.
            return new PlannedAction(
                _policy.ActionKind,
                triggerX,
                triggerX,
                model.CompletionWorldShift,
                model.CompletionWorldShift,
                model.ContextObstacleIndex,
                targetObstacleInstanceId: model.ContextObstacle.InstanceId,
                triggerObstacleInstanceId: model.LastRoof.InstanceId,
                energyCost: 0,
                description: description);
        }
    }
}
