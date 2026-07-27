#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Представляет полностью подготовленное состояние gameplay-экрана без доступа к runtime-сервисам.
    /// </summary>
    internal readonly struct GameplayDevToolsPresentation
    {
        public GameplayDevToolsPresentation(
            string botLabel,
            string unlockAllLabel,
            string status,
            bool botEnabled,
            bool unlockAllLevels,
            bool botActionAvailable,
            bool actionsAvailable)
        {
            BotLabel = botLabel;
            UnlockAllLabel = unlockAllLabel;
            Status = status;
            BotEnabled = botEnabled;
            UnlockAllLevels = unlockAllLevels;
            BotActionAvailable = botActionAvailable;
            ActionsAvailable = actionsAvailable;
        }

        public string BotLabel { get; }
        public string UnlockAllLabel { get; }
        public string Status { get; }
        public bool BotEnabled { get; }
        public bool UnlockAllLevels { get; }
        public bool BotActionAvailable { get; }
        public bool ActionsAvailable { get; }
    }
}
#endif
