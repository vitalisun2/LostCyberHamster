#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using Assets.Scripts.System;
using GameManagement.Progress;
using LostCyberHamster.UI;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Хранит dev-only runtime overrides, которые не должны попадать в сохранение игрока.
    /// </summary>
    public static class DevToolsRuntimeState
    {
        private static HierarchicalLevelCatalog _cachedCatalog;
        private static LevelProgressSnapshot _cachedAllLevelsUnlockedProgress = LevelProgressSnapshot.Empty;
        private static bool _unlockAllLevels;

        static DevToolsRuntimeState()
        {
            LevelManager.SetDevelopmentProgressOverride(
                GetEffectiveProgress,
                () => UnlockAllLevels);
        }

        public static bool UnlockAllLevels
        {
            get => _unlockAllLevels;
            set
            {
                if (_unlockAllLevels == value)
                    return;

                _unlockAllLevels = value;
                UIManager.OnRepaintScreen?.Invoke();
            }
        }

        /// <summary>
        /// Возвращает реальный progress или временный dev snapshot со всеми уровнями из catalog.
        /// </summary>
        public static LevelProgressSnapshot GetEffectiveProgress(
            LevelProgressSnapshot realProgress,
            HierarchicalLevelCatalog catalog)
        {
            if (!UnlockAllLevels || catalog == null || catalog.IsEmpty)
                return realProgress ?? LevelProgressSnapshot.Empty;

            EnsureAllLevelsUnlockedProgress(catalog);
            return _cachedAllLevelsUnlockedProgress;
        }

        private static void EnsureAllLevelsUnlockedProgress(HierarchicalLevelCatalog catalog)
        {
            if (ReferenceEquals(_cachedCatalog, catalog))
                return;

            // Snapshot строится только по уровням, которые реально присутствуют в текущем catalog.
            var entries = catalog.EnumerateLevels()
                .Select(level => new LevelProgressEntry(
                    new LevelProgressKey(level.LocationId, level.PartId, level.LevelIndex),
                    true,
                    LevelProgressEntry.MaxStars))
                .ToList();

            _cachedCatalog = catalog;
            _cachedAllLevelsUnlockedProgress = entries.Count == 0
                ? LevelProgressSnapshot.Empty
                : new LevelProgressSnapshot(entries);
        }
    }
}
#endif
