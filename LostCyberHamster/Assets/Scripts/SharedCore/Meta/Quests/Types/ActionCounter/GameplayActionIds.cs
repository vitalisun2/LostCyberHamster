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

        public static bool IsKnown(string actionId)
        {
            return actionId == ObstacleJumpedOver ||
                   actionId == ObstacleJumpedOn;
        }
    }
}
