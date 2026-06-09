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
}
