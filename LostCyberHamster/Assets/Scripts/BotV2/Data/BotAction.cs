namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Действия бота в этапе 1.
    /// </summary>
    public enum BotAction
    {
        None,
        SwitchLane,  // Сменить линию (TapRequest), 0 энергии
        Jump         // Обычный прыжок (JumpRequest), 10 энергии
    }
}
