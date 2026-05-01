namespace Assets.Scripts.Bot.PlanState
{
    /// <summary>
    /// Перечисляет типы действий, которые может планировать бот.
    /// </summary>
    public enum BotActionKind
    {
        None,
        SwitchLane,
        JumpOver,
        SuperJumpOver,
        JumpOnRoof,
        SuperJumpOnRoof,
        RoofJumpOver,
        SuperRoofJump
    }
}
