using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Содержит runtime-константы для passive roof exit action.
    /// </summary>
    internal sealed class PassiveRoofExitPolicy
    {
        private const string RunFromRoofClipName = "transform_run_from_roof";

        /// <summary>
        /// Возвращает тип действия passive roof exit.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.PassiveRoofExit;

        /// <summary>
        /// Возвращает описание action для логов и diagnostics.
        /// </summary>
        public string DescriptionPrefix => "Passive roof exit";

        /// <summary>
        /// Читает runtime-дистанцию transform_run_from_roof.
        /// </summary>
        public bool TryGetRunFromRoofTravel(out float travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(RunFromRoofClipName, out travel))
                return false;

            return travel > 0f;
        }
    }
}
