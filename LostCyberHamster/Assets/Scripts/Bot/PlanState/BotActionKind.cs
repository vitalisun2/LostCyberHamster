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
        JumpOn,
        SuperJumpOn,
        JumpOnRoof,
        SuperJumpOnRoof,
        RoofJumpOver,
        SuperRoofJumpOver,
        JumpFromRoof,
        SuperJumpFromRoof,
        JumpOnFromRoof,
        SuperJumpOnFromRoof,
        PassiveRoofExit,
        PassiveCollect,
        PassiveAdvance,
        RoofSwitchLane,
        RoofSwitchLaneExit,
        JumpFromRoofOnRoof,
        SuperJumpFromRoofOnRoof
    }
}
