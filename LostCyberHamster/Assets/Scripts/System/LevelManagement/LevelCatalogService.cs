namespace Assets.Scripts.System
{
    /// <summary>
    /// Central access point for the hierarchical level catalog.
    /// </summary>
    public static class LevelCatalogService
    {
        private static HierarchicalLevelCatalog _catalog = HierarchicalLevelCatalog.Empty;

        public static HierarchicalLevelCatalog Catalog => _catalog;

        public static bool HasCatalog => !_catalog.IsEmpty;

        public static void Configure(HierarchicalLevelCatalog catalog)
        {
            _catalog = catalog ?? HierarchicalLevelCatalog.Empty;
        }

        public static void Reset()
        {
            _catalog = HierarchicalLevelCatalog.Empty;
        }

        public static bool TryFindLevel(string identifier, out HierarchicalLevelCatalog.LevelDescriptor descriptor)
        {
            return _catalog.TryFindLevel(identifier, out descriptor);
        }
    }
}
