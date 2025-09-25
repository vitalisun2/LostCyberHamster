namespace Assets.Scripts.System
{
    /// <summary>
    /// Provides access to the current level catalog implementation.
    /// </summary>
    public static class LevelCatalogService
    {
        private static ILevelCatalog _current = new LegacyLevelCatalog();

        public static ILevelCatalog Current => _current;

        /// <summary>
        /// Replaces the current catalog implementation. Intended for feature-flagged scenarios.
        /// </summary>
        public static void SetCatalog(ILevelCatalog catalog)
        {
            _current = catalog ?? _current;
        }
    }
}
