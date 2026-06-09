using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Проверяет применимость ground jump-on к уже выбранному target.
    /// </summary>
    internal sealed class JumpOnSpecification : IBotStrategySpecification
    {
        private readonly IJumpOnPolicy _policy;

        /// <summary>
        /// Создает specification для конкретного jump-on policy.
        /// </summary>
        public JumpOnSpecification(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает true, если ground jump-on policy применима к указанному target.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            // Проверяет planning context и target.
            if (planningState?.Hamster == null
                || obstacle == null)
            {
                return false;
            }

            // Проверяет ground-run состояние и ресурс action.
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

            return ObstacleClassifier.CanJumpOnGroundObstacle(obstacle.ObstacleType);
        }
    }
}
