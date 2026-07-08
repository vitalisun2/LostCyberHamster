namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Описывает одну factual-роль obstacle внутри role-based decision point.
    /// </summary>
    public enum ObstacleRole
    {
        BlockingThreat,
        RoofSupport,
        Target,
        RoofOccupantHazard,
        Collectible
    }

    [global::System.Flags]
    public enum ObstacleRoleMask
    {
        None = 0,
        BlockingThreat = 1 << 0,
        RoofSupport = 1 << 1,
        Target = 1 << 2,
        RoofOccupantHazard = 1 << 3,
        Collectible = 1 << 4
    }
}
