using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof
{
    /// <summary>
    /// Проверяет применимость jump-on-roof к уже выбранному roof support.
    /// </summary>
    internal sealed class JumpOnRoofSpecification : IBotStrategySpecification
    {
        private readonly IJumpOnRoofPolicy _policy;

        /// <summary>
        /// Создает specification для конкретного jump-on-roof policy.
        /// </summary>
        public JumpOnRoofSpecification(IJumpOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает true, если хомяк может прыгнуть на указанную крышу.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            if (planningState?.Hamster == null || obstacle == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.HamsterState != HamsterStateEnum.Run
                || hamster.IsOnRoof
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            return ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType);
        }
    }
}
