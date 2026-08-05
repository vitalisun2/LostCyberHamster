using Assets.Scripts.System;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Предоставляет минимальный каталог для тестов сохранения прогресса игрока.
    /// </summary>
    internal static class PlayerProgressTestCatalog
    {
        public const string FirstLevelAddress =
            "test_location/Morning/level_01";

        /// <summary>
        /// Создаёт каталог с одним игровым уровнем.
        /// </summary>
        public static HierarchicalLevelCatalog Create()
        {
            return HierarchicalLevelCatalog.Factory.CreateCatalog(new[]
            {
                new HierarchicalLevelCatalog.LocationDefinition(
                    "test_location",
                    new[]
                    {
                        new HierarchicalLevelCatalog.PartDefinition(
                            "Morning",
                            new[]
                            {
                                new HierarchicalLevelCatalog.LevelDefinition(
                                    FirstLevelAddress)
                            })
                    })
            });
        }
    }
}
