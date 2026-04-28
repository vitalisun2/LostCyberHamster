namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Перечисляет виды точек решения, которые умеет распознавать planning-слой.
    /// </summary>
    public enum DecisionPointKind
    {
        /// <summary>
        /// Обычное препятствие на текущей линии, требующее реакции без посадки на крышу.
        /// </summary>
        BlockingObstacle,

        /// <summary>
        /// Препятствие на текущей линии, которое можно перепрыгнуть с посадкой на следующую крышу.
        /// </summary>
        BlockingObstacleWithRoofLanding,

        /// <summary>
        /// Безопасная посадка прямо на ближайшую крышу без промежуточного препятствия.
        /// </summary>
        RoofLanding
    }
}