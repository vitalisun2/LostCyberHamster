using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Хранит общее правило high-priority jump-on objective.
    /// </summary>
    internal static class JumpOnObjectiveRules
    {
        /// <summary>
        /// Порог энергии, выше которого planner целенаправленно охотится за jump-on target.
        /// </summary>
        public const int HighPriorityEnergyThreshold = 40;

        /// <summary>
        /// Возвращает true, если энергии больше порога target-oriented jump-on objective.
        /// </summary>
        public static bool HasEnergyForJumpOnObjective(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.Energy > HighPriorityEnergyThreshold;
        }
    }
}
