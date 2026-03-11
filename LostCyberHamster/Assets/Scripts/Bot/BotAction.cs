namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Все возможные действия бота.
    /// </summary>
    public enum BotAction
    {
        None,            // Ничего не делать (бежать)
        SwitchLane,      // Сменить линию (TapRequest)
        Jump,            // Обычный прыжок (JumpRequest, 10 энергии)
        SuperJump,       // Суперпрыжок (SuperJumpRequest, 20 энергии)
        RoofJump,        // Прыжок с крыши (RoofJumpRequest, 10 энергии)
        SuperRoofJump,   // Суперпрыжок с крыши (SuperRoofJumpRequest, 20 энергии)
        UseUlta          // Активировать ульту (UltaEvent)
    }
}
