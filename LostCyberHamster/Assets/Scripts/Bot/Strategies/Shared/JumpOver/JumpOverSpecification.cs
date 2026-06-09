using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOver
{
    /// <summary>
    /// Проверяет применимость ground jump-over к уже выбранной blocking threat.
    /// </summary>
    internal sealed class JumpOverSpecification : IBotStrategySpecification
    {
        private readonly IJumpOverPolicy _policy;

        /// <summary>
        /// Создает specification для конкретного jump-over policy.
        /// </summary>
        public JumpOverSpecification(IJumpOverPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает true, если jump-over policy применима к указанной blocking threat.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            if (planningState?.Hamster == null
                || obstacle == null)
            {
                return false;
            }

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.IsOnRoof
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            return _policy.CanJumpOverObstacle(obstacle.ObstacleType);
        }
    }
}
