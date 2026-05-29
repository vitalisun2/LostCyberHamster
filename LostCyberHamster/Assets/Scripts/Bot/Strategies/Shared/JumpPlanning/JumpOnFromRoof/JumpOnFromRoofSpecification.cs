using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof
{
    /// <summary>
    /// Проверяет применимость roof-to-road jump-on действия к найденной road target-chain.
    /// </summary>
    internal sealed class JumpOnFromRoofSpecification
    {
        /// <summary>
        /// Политика runtime-различий конкретного варианта.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        public JumpOnFromRoofSpecification(IJumpOnFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Проверяет, можно ли планировать напрыгивание с крыши на дорожный target.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleChain actionChain,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex,
            out int targetObstacleChainIndex)
        {
            // Инициализирует пустой результат.
            targetObstacle = null;
            targetObstacleIndex = -1;
            targetObstacleChainIndex = -1;

            // Отсекает неполный вход.
            if (planningState == null
                || actionChain == null
                || lastRoof == null)
            {
                return false;
            }

            // Проверяет состояние хомяка и энергию.
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

            // Ищет первый same-line target внутри road target-chain.
            if (!actionChain.TryFindFirstJumpOnFromRoofTarget(
                    hamster.IsOnBottomLine,
                    out targetObstacle,
                    out targetObstacleIndex,
                    out targetObstacleChainIndex))
            {
                return false;
            }

            // Проверяет, есть ли причина планировать roof jump-on.
            return CanPlanJumpOnFromRoof(
                hamster,
                actionChain.FirstObstacle,
                lastRoof,
                travel);
        }

        /// <summary>
        /// Проверяет, есть ли причина планировать roof jump-on target.
        /// </summary>
        private static bool CanPlanJumpOnFromRoof(
            HamsterSnapshot hamster,
            ObstacleSnapshot firstRoadObstacle,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel)
        {
            // если энергии избыток, то пробуем планировать
            if (JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster))
                return true;

            // если простой автоматический сход опасен, то всё равно пробуем планировать
            if (IsDangerousAutomaticRoofExit(firstRoadObstacle, lastRoof, travel))
                return true;

            return false;
        }

        /// <summary>
        /// Возвращает true, если простой автоматический сход с крыши попадёт в ближайшую road-chain.
        /// </summary>
        private static bool IsDangerousAutomaticRoofExit(
            ObstacleSnapshot firstRoadObstacle,
            ObstacleSnapshot lastRoof,
            JumpOnFromRoofTravel travel)
        {
            if (firstRoadObstacle == null || lastRoof == null)
                return false;

            float gapToFirstRoadObstacle = firstRoadObstacle.LeftX - lastRoof.RightX;
            return gapToFirstRoadObstacle < travel.RunFromRoofTravel;
        }
    }
}
