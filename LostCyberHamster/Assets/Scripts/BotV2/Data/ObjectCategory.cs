namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Категория объекта для принятия решений ботом.
    /// </summary>
    public enum ObjectCategory
    {
        Threat,      // Угроза (столкновение = урон)
        Target,      // Цель высокого приоритета (можно напрыгнуть)
        Collectible, // Бонус (энергия, жизнь, монеты)
        Neutral      // Декор или объекты позади хомяка
    }
}
