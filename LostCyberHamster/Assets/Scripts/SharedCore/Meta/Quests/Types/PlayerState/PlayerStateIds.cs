namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Стабильные идентификаторы постоянных состояний игрока.
    /// </summary>
    public static class PlayerStateIds
    {
        public const string SkinOwned = "skin_owned";
        public const string SkinApplied = "skin_applied";
        public const string PlayerLevel = "player_level";
        public const string SuperAttackActive = "super_attack_active";

        /// <summary>
        /// Проверяет поддержку идентификатора состояния.
        /// </summary>
        public static bool IsKnown(string stateId)
        {
            return stateId == SkinOwned ||
                   stateId == SkinApplied ||
                   stateId == PlayerLevel ||
                   stateId == SuperAttackActive;
        }
    }
}
