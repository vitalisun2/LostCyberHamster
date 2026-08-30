namespace Vues.GameCore
{
    /// <summary>
    /// Хранит стабильные идентификаторы и retired-диапазон скинов.
    /// </summary>
    public static class SkinIdentity
    {
        public const int DefaultId = 0;
        public const int FirstActiveSkinId = 4;

        /// <summary>
        /// Проверяет идентификатор удалённого legacy-скина.
        /// </summary>
        public static bool IsRetired(int skinId)
        {
            return skinId is 1 or 2 or 3;
        }
    }
}
