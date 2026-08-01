namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Стабильные идентификаторы игровых действий для квестов-счётчиков.
    /// </summary>
    public static class GameplayActionIds
    {
        public const string ObstacleJumpedOver =
            "obstacle_jumped_over";
        public const string ObstacleJumpedOn =
            "obstacle_jumped_on";
        public const string ObstacleJumpedOnFromRoof =
            "obstacle_jumped_on_from_roof";
        public const string VehicleRoofRunCompleted =
            "vehicle_roof_run_completed";
        public const string RoofToRoofJump =
            "roof_to_roof_jump";

        public static bool IsKnown(string actionId)
        {
            return actionId == ObstacleJumpedOver ||
                   actionId == ObstacleJumpedOn ||
                   actionId == ObstacleJumpedOnFromRoof ||
                   actionId == VehicleRoofRunCompleted ||
                   actionId == RoofToRoofJump;
        }
    }
}
