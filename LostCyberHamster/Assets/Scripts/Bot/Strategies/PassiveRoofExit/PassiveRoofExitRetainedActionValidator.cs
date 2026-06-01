using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Перепроверяет сохраненный passive roof exit action на границе retained-префикса.
    /// </summary>
    internal sealed class PassiveRoofExitRetainedActionValidator : IRetainedActionValidator
    {
        private readonly PassiveRoofExitPolicy _policy;

        public PassiveRoofExitRetainedActionValidator(PassiveRoofExitPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает тип действия passive roof exit.
        /// </summary>
        public BotActionKind ActionKind => _policy.ActionKind;

        /// <summary>
        /// Проверяет, что roof-chain, target context и safety gate всё ещё актуальны.
        /// </summary>
        public bool IsStillValid(RetainedActionContext context)
        {
            if (context == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || !context.Action.TargetObstacleInstanceId.HasValue
                || !context.Action.TriggerObstacleInstanceId.HasValue)
            {
                return false;
            }

            if (!_policy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return false;

            if (!PassiveRoofExitPlanner.TryBuildModel(
                    context.PlanningState,
                    context.ProjectedWorldSnapshot,
                    context.DecisionPoint,
                    runFromRoofTravel,
                    out PassiveRoofExitModel model))
            {
                return false;
            }

            return model.ContextObstacle.InstanceId == context.Action.TargetObstacleInstanceId.Value
                && model.LastRoof.InstanceId == context.Action.TriggerObstacleInstanceId.Value;
        }
    }
}
