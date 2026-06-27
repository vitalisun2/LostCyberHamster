namespace Assets.Scripts.Bot.PlanState
{
    /// <summary>
    /// Хранит общие правила классификации action kind для planning-метрик.
    /// </summary>
    internal static class BotActionKindRules
    {
        /// <summary>
        /// Возвращает true, если действие требует нажатия.
        /// </summary>
        public static bool ConsumesTap(BotActionKind kind)
        {
            return kind == BotActionKind.SwitchLane
                || kind == BotActionKind.RoofSwitchLane
                || kind == BotActionKind.RoofSwitchLaneExit;
        }
    }
}
