using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Выбирает roof occupant hazard из role-based chain.
    /// </summary>
    internal sealed class RoofJumpOverActionResolver
    {
        /// <summary>
        /// Возвращает первый roof occupant hazard текущей role-based ситуации.
        /// </summary>
        public bool TryResolve(
            ObstacleChain chain,
            out ObstacleSnapshot hazardObstacle,
            out int hazardObstacleIndex)
        {
            // Готовит fallback-результат.
            hazardObstacle = null;
            hazardObstacleIndex = -1;

            // Проверяет наличие chain.
            if (chain == null)
                return false;

            // Проверяет, что chain начинается с roof occupant hazard.
            ObstacleChainElement hazardElement = chain.First;
            if (!hazardElement.HasRole(ObstacleRole.RoofOccupantHazard))
                return false;

            // Возвращает obstacle и его world index.
            hazardObstacle = hazardElement.Obstacle;
            hazardObstacleIndex = hazardElement.WorldIndex;
            return true;
        }
    }
}
