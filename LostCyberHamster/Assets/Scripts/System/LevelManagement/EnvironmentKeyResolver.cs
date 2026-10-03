using System;
using GameManagement;

namespace Assets.Scripts.System.LevelManagement
{
    /// <summary>Разрешает точный адрес готового окружения по локации и времени суток.</summary>
    public static class EnvironmentKeyResolver
    {
        /// <summary>Строит адрес готового префаба для указанной локации и времени суток.</summary>
        public static string BuildEnvironmentKey(string locationId, string daypart)
        {
            var locationSlug = LocationAssetFallback.ToLocationSlug(locationId);
            var daypartSlug = LocationAssetFallback.ToSlug(daypart);
            if (string.IsNullOrWhiteSpace(locationSlug) || string.IsNullOrWhiteSpace(daypartSlug))
                throw new ArgumentException($"Не заданы локация '{locationId}' или время суток '{daypart}'.");
            return $"environment_{locationSlug}_{daypartSlug}";
        }

        /// <summary>Строит адрес окружения текущего уровня из каталога или полного адреса уровня.</summary>
        public static string BuildEnvironmentKey()
        {
            // Каталог содержит канонические идентификаторы текущего уровня.
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (LevelCatalogService.TryFindLevel(currentLevel, out var descriptor))
                return BuildEnvironmentKey(descriptor.LocationId, descriptor.PartId);

            // Уровни инструментов вне каталога сохраняют локацию и время суток в своём адресе.
            if (HierarchicalLevelCatalog.TryParseLevelAddress(currentLevel, out var locationId, out var daypart, out _))
                return BuildEnvironmentKey(locationId, daypart);
            throw new InvalidOperationException($"Не удалось определить окружение для уровня '{currentLevel}': нужны локация и время суток.");
        }
    }
}
