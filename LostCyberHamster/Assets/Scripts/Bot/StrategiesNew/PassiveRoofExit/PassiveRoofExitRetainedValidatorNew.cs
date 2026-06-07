using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Planning.RetainedValidation;

namespace Assets.Scripts.Bot.StrategiesNew.PassiveRoofExit
{
    /// <summary>
    /// Перепроверяет сохраненный role-based passive roof exit action.
    /// </summary>
    internal sealed class PassiveRoofExitRetainedValidatorNew : IRetainedActionValidatorNew
    {
        /// <summary>
        /// Policy passive roof exit action.
        /// </summary>
        private readonly PassiveRoofExitPolicy _policy;

        /// <summary>
        /// Detector для восстановления актуальной role-based planning-ситуации.
        /// </summary>
        private readonly DecisionPointDetectorNew _decisionPointDetector = new DecisionPointDetectorNew();

        public PassiveRoofExitRetainedValidatorNew(PassiveRoofExitPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает тип действия passive roof exit.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, что roof-chain, context obstacle и safety gate всё ещё актуальны.
        /// </summary>
        public bool IsStillValid(RetainedActionContextNew context)
        {
            // Проверяет retained context.
            if (context?.PlanningState == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || !context.Action.TargetObstacleInstanceId.HasValue
                || !context.Action.TriggerObstacleInstanceId.HasValue)
            {
                return false;
            }

            // Перестраивает текущий decision point и model.
            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel)
                || !_decisionPointDetector.TryDetect(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    out DecisionPointNew decisionPoint)
                || !PassiveRoofExitPlannerNew.TryBuildModel(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    decisionPoint,
                    runFromRoofTravel,
                    out PassiveRoofExitModel model))
            {
                return false;
            }

            // Сверяет retained anchors.
            return model.ContextObstacle.InstanceId == context.Action.TargetObstacleInstanceId.Value
                && model.ContextObstacle.InstanceId == context.RetainedObstacle.InstanceId
                && model.LastRoof.InstanceId == context.Action.TriggerObstacleInstanceId.Value;
        }
    }
}
