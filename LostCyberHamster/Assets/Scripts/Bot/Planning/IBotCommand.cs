namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Интерфейс команды для forward simulation (Command pattern).
    /// Каждая команда умеет применить себя к SimWorldState.
    /// </summary>
    public interface IBotCommand
    {
        /// <summary>
        /// Действие бота, соответствующее этой команде.
        /// </summary>
        BotAction Action { get; }

        /// <summary>
        /// Может ли команда быть выполнена в текущем состоянии?
        /// </summary>
        bool CanExecute(ref SimWorldState state);

        /// <summary>
        /// Применяет команду к состоянию (мутирует state).
        /// </summary>
        void Execute(ref SimWorldState state);
    }
}
