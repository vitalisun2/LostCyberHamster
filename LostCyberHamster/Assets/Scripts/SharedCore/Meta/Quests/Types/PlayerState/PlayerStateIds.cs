namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Стабильные идентификаторы постоянных состояний игрока.
    /// </summary>
    public static class PlayerStateIds
    {
        public const string SkinOwned = "skin_owned";

        /// <summary>
        /// Проверяет поддержку идентификатора состояния.
        /// </summary>
        public static bool IsKnown(string stateId)
        {
            return stateId == SkinOwned;
        }
    }
}
