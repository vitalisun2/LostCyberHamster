using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Находит опасные obstacles, которые стоят на текущем passive roof path хомяка.
    /// </summary>
    internal sealed class RoofOccupantHazardDetector
    {
        /// <summary>
        /// Пытается найти первый roof occupant hazard на текущей цепочке крыш.
        /// </summary>
        public bool TryDetect(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out int firstHazardIndex)
        {
            // Инициализирует пустой результат.
            firstHazardIndex = -1;

            // Отсекает неполный и не roof-вход.
            if (planningState == null || worldSnapshot == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.IsOnRoof)
                return false;

            // Ищет первый опасный occupant на текущем passive roof path.
            // Не опирается на NextObstacleIndex: roof landing может продвинуть индекс мимо occupant'а на крыше.
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                        planningState,
                        worldSnapshot,
                        obstacle,
                        out _,
                        out _))
                {
                    continue;
                }

                firstHazardIndex = obstacleIndex;
                return true;
            }

            return false;
        }
    }
}
