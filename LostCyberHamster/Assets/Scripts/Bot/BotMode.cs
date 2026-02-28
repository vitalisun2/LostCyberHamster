namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Режим работы бота.
    /// </summary>
    public enum BotMode
    {
        /// <summary>Обычная автоигра — бот играет за игрока.</summary>
        Play,

        /// <summary>Тестирование механик — бот целенаправленно проверяет все взаимодействия.</summary>
        Test,

        /// <summary>Аналитика — бот играет нормально с расширенным логированием.</summary>
        Analytics
    }
}
