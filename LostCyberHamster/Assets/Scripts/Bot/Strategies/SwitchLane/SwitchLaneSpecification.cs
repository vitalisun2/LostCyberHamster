using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SwitchLane
{
    /// <summary>
    /// Проверяет применимость смены линии для текущей planning-ситуации.
    /// </summary>
    internal sealed class SwitchLaneSpecification
    {
        /// <summary>
        /// Проверяет, можно ли построить действие смены линии для текущей chain-точки решения.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex)
        {
            // Сбрасывает выходные значения перед проверкой.
            targetObstacle = null;
            targetObstacleIndex = -1;

            // Проверяет наличие обязательного planning-контекста.
            if (planningState == null
                || decisionPoint == null
                || decisionPoint.Chain == null)
            {
                return false;
            }

            // Отбрасывает состояния, в которых смену линии планировать нельзя.
            HamsterSnapshot hamster = planningState.Hamster;
            if (!CanPlanSwitchLaneFromState(hamster.HamsterState)
                || hamster.IsShifting)
            {
                return false;
            }

            // Проверяет, что первый obstacle цепочки требует ground-уклонения.
            ObstacleSnapshot firstObstacle = decisionPoint.Chain.FirstObstacle;
            if (!ObstacleClassifier.DamagesOnGroundContact(firstObstacle.ObstacleType))
            {
                return false;
            }

            // Возвращает obstacle-цель для планируемого действия.
            targetObstacle = firstObstacle;
            targetObstacleIndex = decisionPoint.Chain.FirstIndex;
            return true;
        }

        /// <summary>
        /// Проверяет, разрешено ли в принципе планировать смену линии в текущем состоянии хомяка.
        /// </summary>
        public bool IsSatisfiedBy(PlanningState planningState)
        {
            // Отбрасывает отсутствие planning-состояния.
            if (planningState == null)
                return false;

            // Проверяет состояние хомяка на допустимость планирования.
            HamsterSnapshot hamster = planningState.Hamster;
            return CanPlanSwitchLaneFromState(hamster.HamsterState)
                && !hamster.IsShifting;
        }

        /// <summary>
        /// Определяет, допускает ли текущее runtime-состояние запуск планирования смены линии.
        /// </summary>
        private static bool CanPlanSwitchLaneFromState(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.Run
                   || hamsterState == HamsterStateEnum.RoofRun;
        }
    }
}
