using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.PassiveRoofExit
{
    /// <summary>
    /// Строит role-based planning-модель безопасного пассивного схода с крыши.
    /// </summary>
    internal static class PassiveRoofExitPlanner
    {
        /// <summary>
        /// Возвращает модель passive roof exit, если её можно безопасно рассмотреть в текущем context.
        /// </summary>
        public static bool TryBuildModel(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            float runFromRoofTravel,
            out PassiveRoofExitModel model,
            out string deadEndReason)
        {
            // Проверяет входные данные и состояние хомяка.
            model = default;
            deadEndReason = null;
            if (planningState == null || worldSnapshot == null || runFromRoofTravel <= 0f)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (!CanExitRoofPassively(hamster))
                return false;

            // Получает context obstacle из role-based chain.
            if (!TryGetContextObstacle(
                    decisionPoint,
                    hamster,
                    out ObstacleSnapshot contextObstacle,
                    out int contextObstacleIndex,
                    out deadEndReason))
            {
                return false;
            }

            // Находит последнюю roof текущей passive roof chain.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out _))
            {
                return false;
            }

            // Проверяет безопасность автоматического схода.
            if (!RoofExitSafety.TryGetRunFromRoofWindow(
                    hamster,
                    lastRoof,
                    runFromRoofTravel,
                    out float exitStartShift,
                    out float completionWorldShift))
            {
                return false;
            }

            if (!RoofExitSafety.IsSafeDuringRunFromRoof(
                    hamster,
                    worldSnapshot,
                    hamster.IsOnBottomLine,
                    exitStartShift,
                    completionWorldShift,
                    out deadEndReason))
            {
                return false;
            }

            // Возвращает готовую модель transition.
            model = new PassiveRoofExitModel(
                lastRoof,
                contextObstacle,
                contextObstacleIndex,
                exitStartShift,
                completionWorldShift);
            return true;
        }

        /// <summary>
        /// Проверяет, находится ли хомяк в состоянии, допускающем passive roof exit.
        /// </summary>
        private static bool CanExitRoofPassively(HamsterSnapshot hamster)
        {
            // Проверяет roof-run состояние.
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.RoofSupportInstanceId.HasValue;
        }

        /// <summary>
        /// Возвращает context obstacle, относительно которого строится продолжение после схода.
        /// </summary>
        private static bool TryGetContextObstacle(
            DecisionPoint decisionPoint,
            HamsterSnapshot hamster,
            out ObstacleSnapshot contextObstacle,
            out int contextObstacleIndex,
            out string deadEndReason)
        {
            // Сбрасывает результат и проверяет chain.
            contextObstacle = null;
            contextObstacleIndex = -1;
            deadEndReason = null;
            ObstacleChainElement firstElement = decisionPoint?.Chain?.First;
            if (firstElement == null)
                return false;

            // Не позволяет passive exit обходить hazard на текущей roof path.
            if (firstElement.HasRole(ObstacleRole.RoofOccupantHazard))
            {
                deadEndReason = "Нет безопасного пассивного продолжения: на текущей крыше находится опасное препятствие.";
                return false;
            }

            // Проверяет, что context obstacle ещё актуален впереди.
            contextObstacle = firstElement.Obstacle;
            if (contextObstacle == null || contextObstacle.RightX <= hamster.HamsterLeftX)
                return false;

            contextObstacleIndex = firstElement.WorldIndex;
            return true;
        }
    }
}
