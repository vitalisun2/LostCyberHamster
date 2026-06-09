using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Проверяет применимость roof-to-road jump-on к уже выбранному target.
    /// </summary>
    internal sealed class JumpOnFromRoofSpecification : IBotStrategySpecification
    {
        /// <summary>
        /// Policy конкретного варианта roof-to-road jump-on.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        public JumpOnFromRoofSpecification(IJumpOnFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает true, если хомяк может выполнить roof-to-road jump-on по указанному target.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            // Проверяет planning context и target.
            if (planningState?.Hamster == null || obstacle == null)
                return false;

            // Проверяет roof-run состояние и ресурс action.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.HamsterState != HamsterStateEnum.RoofRun
                || !hamster.IsOnRoof
                || !hamster.RoofSupportInstanceId.HasValue
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            // Проверяет, что target находится на текущей линии и подходит для roof-to-road jump-on.
            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            return ObstacleClassifier.CanJumpOnFromRoofObstacle(obstacle.ObstacleType);
        }
    }
}
