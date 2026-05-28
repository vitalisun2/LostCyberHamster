using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Проверяет применимость ground jump-on действия.
    /// </summary>
    internal sealed class JumpOnSpecification
    {
        /// <summary>
        /// Политика runtime-различий конкретного jump-on варианта.
        /// </summary>
        private readonly IJumpOnPolicy _policy;

        public JumpOnSpecification(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Проверяет, можно ли выполнить jump-on по указанной chain, и возвращает первый валидный target.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleChain chain,
            out ObstacleSnapshot targetObstacle,
            out int targetObstacleIndex)
        {
            // Инициализирует выходные значения.
            targetObstacle = null;
            targetObstacleIndex = -1;

            // Проверяет наличие planning context.
            if (planningState == null
                || chain == null)
            {
                return false;
            }

            // Проверяет состояние хомяка и энергию.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null
                || hamster.IsOnRoof
                || hamster.IsShifting
                || hamster.Energy < _policy.EnergyCost)
            {
                return false;
            }

            // Ищет первый ground jump-on target в chain.
            if (!chain.TryFindFirstGroundJumpOnTarget(
                    hamster.IsOnBottomLine,
                    out targetObstacle,
                    out targetObstacleIndex,
                    out _))
            {
                return false;
            }

            return true;
        }
    }
}
