using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Обнаруживает planning-ситуации, где само движение приводит к смене planning state.
    /// </summary>
    internal sealed class MovingBoundaryDecisionPointDetector
    {
        /// <summary>
        /// Возвращает moving boundary для естественного схода с крыши.
        /// </summary>
        public bool TryDetectPassiveRoofExit(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Проверяет базовое roof-run состояние.
            decisionPoint = null;
            if (!CanDetectPassiveRoofExit(planningState, worldSnapshot))
                return false;

            // Подтверждает наличие текущей passive roof chain.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out _,
                    out _))
            {
                return false;
            }

            // Не поднимает boundary, если на текущем roof path есть обязательная roof-ситуация.
            if (HasDamagingOccupantOnPassiveRoofPath(planningState, worldSnapshot))
                return false;

            decisionPoint = DecisionPoint.MovingBoundary(MovingBoundaryKind.PassiveRoofExit);
            return true;
        }

        /// <summary>
        /// Проверяет, можно ли искать moving boundary пассивного схода с крыши.
        /// </summary>
        private static bool CanDetectPassiveRoofExit(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            HamsterSnapshot hamster = planningState?.Hamster;
            return hamster != null
                && worldSnapshot?.Obstacles != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && hamster.RoofSupportInstanceId.HasValue
                && !hamster.IsShifting;
        }

        /// <summary>
        /// Возвращает true, если текущий passive roof path содержит опасного occupant-а.
        /// </summary>
        private static bool HasDamagingOccupantOnPassiveRoofPath(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                if (RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                        planningState,
                        worldSnapshot,
                        worldSnapshot.Obstacles[obstacleIndex],
                        out _,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
