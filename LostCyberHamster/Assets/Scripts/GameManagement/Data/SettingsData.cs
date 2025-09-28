using System;

namespace GameManagement
{
    [Serializable]
    public class SettingsData
    {
        /// <summary>
        /// Язык игры.
        /// </summary>
        public int Language;

        /// <summary>
        /// Громкость музыки.
        /// </summary>
        public float MusicVolume = 1;

        /// <summary>
        /// Громкость звуковых эффектов.
        /// </summary>
        public float SfxVolume = 1;

        /// <summary>
        /// Включена ли вибрация.
        /// </summary>
        public bool EnableVibration = true;

        /// <summary>
        /// Флаг включения уровней по времени суток.
        /// </summary>
        public bool EnableDayPartLevels;
    }
}
