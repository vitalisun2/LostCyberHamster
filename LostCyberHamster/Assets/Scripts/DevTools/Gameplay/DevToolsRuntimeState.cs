#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using Assets.Scripts.System;
using GameManagement.Progress;
using LostCyberHamster.UI;
using UnityEngine;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Хранит dev-only runtime overrides, которые не должны попадать в сохранение игрока.
    /// </summary>
    public static class DevToolsRuntimeState
    {
        private static HierarchicalLevelCatalog _cachedCatalog;
        private static LevelProgressSnapshot _cachedRealProgress;
        private static LevelProgressSnapshot _cachedAllLevelsUnlockedProgress = LevelProgressSnapshot.Empty;
        private static bool _unlockAllLevels;

        static DevToolsRuntimeState()
        {
            LevelManager.SetDevelopmentProgressOverride(
                GetEffectiveProgress,
                () => UnlockAllLevels);
        }

        /// <summary>Начинает каждый запуск игры с реального доступа и звёзд игрока.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _unlockAllLevels = false;
            _cachedCatalog = null;
            _cachedRealProgress = null;
            _cachedAllLevelsUnlockedProgress = LevelProgressSnapshot.Empty;
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
        /// Временно открывает уровни каталога, сохраняя реальные звёзды игрока.
        /// </summary>
        public static LevelProgressSnapshot GetEffectiveProgress(
            LevelProgressSnapshot realProgress,
            HierarchicalLevelCatalog catalog)
        {
            if (!UnlockAllLevels || catalog == null || catalog.IsEmpty)
                return realProgress ?? LevelProgressSnapshot.Empty;

            EnsureAllLevelsUnlockedProgress(catalog, realProgress ?? LevelProgressSnapshot.Empty);
            return _cachedAllLevelsUnlockedProgress;
        }

        /// <summary>Обновляет доступ уровней при изменении каталога или сохранённого прогресса.</summary>
        private static void EnsureAllLevelsUnlockedProgress(
            HierarchicalLevelCatalog catalog,
            LevelProgressSnapshot realProgress)
        {
            if (ReferenceEquals(_cachedCatalog, catalog) && ReferenceEquals(_cachedRealProgress, realProgress))
                return;

            // Открытие меняет доступ, а звёзды и адрес принадлежат реальному уровню.
            var entries = catalog.EnumerateLevels()
                .Select(level =>
                {
                    var key = new LevelProgressKey(level.LocationId, level.PartId, level.LevelIndex);
                    return new LevelProgressEntry(key, true, realProgress.GetStars(key), level.Address);
                })
                .ToList();

            // Кеш относится к конкретным неизменяемым входным снимкам.
            _cachedCatalog = catalog;
            _cachedRealProgress = realProgress;
            _cachedAllLevelsUnlockedProgress = entries.Count == 0
                ? LevelProgressSnapshot.Empty
                : new LevelProgressSnapshot(entries);
        }
    }
}
#endif
