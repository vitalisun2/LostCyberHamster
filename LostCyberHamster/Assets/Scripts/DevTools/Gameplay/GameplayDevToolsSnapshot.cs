#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Содержит актуальную доступность и состояние gameplay-инструментов для отображения в DEV UI.
    /// </summary>
    internal readonly struct GameplayDevToolsSnapshot
    {
        public GameplayDevToolsSnapshot(
            bool botAvailable,
            bool botEnabled,
            bool unlockAllLevels)
        {
            BotAvailable = botAvailable;
            BotEnabled = botEnabled;
            UnlockAllLevels = unlockAllLevels;
        }

        public bool BotAvailable { get; }
        public bool BotEnabled { get; }
        public bool UnlockAllLevels { get; }
    }
}
#endif
