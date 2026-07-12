#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Описывает итог gameplay-действия и необходимость закрыть DEV-панель после успеха.
    /// </summary>
    internal readonly struct GameplayDevToolsActionResult
    {
        private GameplayDevToolsActionResult(bool succeeded, string message, bool closePanel)
        {
            Succeeded = succeeded;
            Message = message;
            ClosePanel = closePanel;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public bool ClosePanel { get; }

        public static GameplayDevToolsActionResult Success(string message, bool closePanel = false)
        {
            return new GameplayDevToolsActionResult(true, message, closePanel);
        }

        public static GameplayDevToolsActionResult Unavailable(string message)
        {
            return new GameplayDevToolsActionResult(false, message, false);
        }
    }
}
#endif
