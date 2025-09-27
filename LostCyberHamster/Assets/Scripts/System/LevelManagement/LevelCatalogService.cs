namespace Assets.Scripts.System
{
    /// <summary>
    /// Provides access to the current level catalog implementation.
    /// </summary>
    public static class LevelCatalogService
    {
        private static readonly ILevelCatalog LegacyCatalog = new LegacyLevelCatalog();
        private static ILevelCatalog _current = LegacyCatalog;
        private static HierarchicalLevelCatalog? _hierarchical;

        public static ILevelCatalog Current => _current;

        /// <summary>
        /// Shows whether the hierarchical catalog is currently active.
        /// </summary>
        public static bool IsHierarchical => _hierarchical != null && ReferenceEquals(_current, _hierarchical);

        /// <summary>
        /// Returns the registered hierarchical catalog instance (if any).
        /// </summary>
        public static HierarchicalLevelCatalog? Hierarchical => _hierarchical;

        /// <summary>
        /// Returns true when a hierarchical catalog has been configured.
        /// </summary>
        public static bool HasHierarchicalCatalog => _hierarchical != null;

        /// <summary>
        /// Replaces the current catalog implementation. Intended for feature-flagged scenarios.
        /// Preserves a reference to hierarchical implementations for toggling.
        /// </summary>
        public static void SetCatalog(ILevelCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            _current = catalog;

            if (catalog is HierarchicalLevelCatalog hierarchical)
            {
                _hierarchical = hierarchical;
            }
        }

        /// <summary>
        /// Configures the hierarchical catalog without immediately activating it.
        /// </summary>
        public static void ConfigureHierarchicalCatalog(HierarchicalLevelCatalog catalog, bool activate = false)
        {
            _hierarchical = catalog;

            if (activate)
            {
                UseHierarchicalCatalog();
            }
        }

        /// <summary>
        /// Switches the service to use the hierarchical catalog if it is configured.
        /// </summary>
        public static bool UseHierarchicalCatalog()
        {
            if (_hierarchical == null)
            {
                return false;
            }

            _current = _hierarchical;
            return true;
        }

        /// <summary>
        /// Switches the service back to the legacy catalog implementation.
        /// </summary>
        public static void UseLegacyCatalog()
        {
            _current = LegacyCatalog;
        }
    }
}
