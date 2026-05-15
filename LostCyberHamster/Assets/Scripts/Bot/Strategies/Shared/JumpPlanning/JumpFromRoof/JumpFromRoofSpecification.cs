using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Проверяет применимость прыжка с крыши на дорогу перед опасным road obstacle.
    /// </summary>
    internal sealed class JumpFromRoofSpecification
    {
        /// <summary>
        /// Хранит runtime-отличия конкретного варианта прыжка с крыши.
        /// </summary>
        private readonly IJumpFromRoofPolicy _policy;

        public JumpFromRoofSpecification(IJumpFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Проверяет, можно ли планировать прыжок с крыши для текущего decision point.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            DecisionPoint decisionPoint,
            JumpFromRoofTravel travel,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out ObstacleSnapshot lastRoof)
        {
            // Инициализирует пустой результат.
            targetObstacle = null;
            targetObstacleIndex = -1;
            lastRoof = null;

            // Отсекает неполный вход.
            if (planningState == null
                || projectedWorldSnapshot == null
                || decisionPoint?.Chain == null)
            {
                return false;
            }

            // Проверяет состояние хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null
                || hamster.HamsterState != HamsterStateEnum.RoofRun
                || !hamster.IsOnRoof
                || !hamster.RoofSupportInstanceId.HasValue
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            // Проверяет первый obstacle chain.
            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (ObstacleClassifier.IsObstacleWithRoof(firstObstacle.ObstacleType))
                return false;

            if (firstObstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (!ObstacleClassifier.DamagesOnGroundContact(firstObstacle.ObstacleType))
                return false;

            // Не перехватывает roof occupant: это зона ответственности roof jump-over strategies.
            if (RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                    planningState,
                    projectedWorldSnapshot,
                    firstObstacle,
                    out _,
                    out _))
            {
                return false;
            }

            // Находит последнюю passive roof.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out lastRoof,
                    out _))
            {
                return false;
            }

            // Проверяет опасность автоматического схода.
            float gap = firstObstacle.LeftX - lastRoof.RightX;
            if (gap >= travel.RunFromRoofTravel)
                return false;

            // Возвращает target obstacle.
            targetObstacle = firstObstacle;
            targetObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }
    }
}
