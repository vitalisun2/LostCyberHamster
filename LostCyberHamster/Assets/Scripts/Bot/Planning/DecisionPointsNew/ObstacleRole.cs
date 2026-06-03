using System;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Описывает factual-роли obstacle внутри role-based decision point.
    /// </summary>
    [Flags]
    public enum ObstacleRole
    {
        None = 0,
        BlockingThreat = 1 << 0,
        RoofSupport = 1 << 1,
        Target = 1 << 2,
        RoofOccupantHazard = 1 << 3,
        Collectible = 1 << 4
    }
}
