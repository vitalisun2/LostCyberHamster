namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Действия бота BotV2.
    /// </summary>
    public enum BotAction
    {
        None,
        SwitchLane,  // Сменить линию (TapRequest), 0 энергии
        Jump,        // Обычный прыжок (JumpRequest), 10 энергии
        SuperJump    // Суперпрыжок (SuperJumpRequest), 20 энергии
    }
}
