using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies
{
    /// <summary>
    /// Рассчитывает физически допустимое окно поиска fire shift для jump-действия.
    /// </summary>
    internal interface IJumpSearchWindowPolicy
    {
        bool TryGetSearchWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            out float firstFireShift,
            out float lastFireShift);
    }
}
