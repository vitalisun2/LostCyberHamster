using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Содержит runtime-константы для role-based passive roof exit action.
    /// </summary>
    internal sealed class PassiveRoofExitPolicy
    {
        /// <summary>
        /// Имя animation clip автоматического схода с крыши.
        /// </summary>
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
        /// Читает runtime-дистанцию автоматического схода с крыши.
        /// </summary>
        public bool TryGetRunFromRoofTravel(out float travel)
        {
            // Считывает travel clip из runtime cache.
            if (!BotAnimationTravelProvider.TryGetTravel(RunFromRoofClipName, out travel))
                return false;

            // Подтверждает валидную дистанцию.
            return travel > 0f;
        }
    }
}
