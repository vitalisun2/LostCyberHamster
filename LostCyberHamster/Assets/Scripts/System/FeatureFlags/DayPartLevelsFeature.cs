using System;
using GameManagement;
using UnityEngine;

namespace Assets.Scripts.System.FeatureFlags
{
    /// <summary>
    /// Centralised toggle for the DayPart levels feature flag.
    /// Wraps access to <see cref="SettingsData.EnableDayPartLevels"/> and persists changes via <see cref="GameDataManager"/>.
    /// </summary>
    public static class DayPartLevelsFeature
    {
        private static bool _isEnabled;
        private static bool _isInitialised;

        public static bool IsEnabled => _isEnabled;

        public static event Action<bool> OnFeatureChanged;

        public static void InitializeFromSettings(SettingsData settings)
        {
            if (settings == null)
            {
                SetEnabledInternal(false, false);
                return;
            }

            SetEnabledInternal(settings.EnableDayPartLevels, false);
        }

        public static void SetEnabled(bool enabled, bool persist = true)
        {
            SetEnabledInternal(enabled, persist);
        }

        public static void EnsureInitialised()
        {
            if (_isInitialised)
            {
                return;
            }

            InitializeFromSettings(GameDataManager.Settings);
        }

        private static void SetEnabledInternal(bool enabled, bool persist)
        {
            if (_isInitialised && _isEnabled == enabled)
            {
                return;
            }

            _isEnabled = enabled;
            _isInitialised = true;

            var settings = GameDataManager.Settings ?? new SettingsData();
            settings.EnableDayPartLevels = enabled;
            GameDataManager.Settings = settings;

            if (persist)
            {
                GameDataManager.SaveSettings();
            }

            Debug.Log($"[DayPartLevelsFeature] Day-part levels {(enabled ? "ENABLED" : "DISABLED")}");
            OnFeatureChanged?.Invoke(enabled);
        }
    }
}
