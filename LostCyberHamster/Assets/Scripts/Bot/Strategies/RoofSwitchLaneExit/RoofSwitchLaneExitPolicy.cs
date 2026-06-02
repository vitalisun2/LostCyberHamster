using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Хранит policy для схода с крыши через смену линии.
    /// </summary>
    internal sealed class RoofSwitchLaneExitPolicy
    {
        private const string RunFromRoofClipName = "transform_run_from_roof";

        public BotActionKind ActionKind => BotActionKind.RoofSwitchLaneExit;

        public string DescriptionPrefix => "Roof switch-lane exit";

        /// <summary>
        /// Читает runtime-дистанцию transform_run_from_roof.
        /// </summary>
        public bool TryGetRunFromRoofTravel(out float travel)
        {
            return BotAnimationTravelProvider.TryGetTravel(RunFromRoofClipName, out travel)
                && travel > 0f;
        }
    }
}
