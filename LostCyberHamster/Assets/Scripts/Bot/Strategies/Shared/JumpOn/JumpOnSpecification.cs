using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Проверяет применимость ground jump-on к уже выбранному target.
    /// </summary>
    internal sealed class JumpOnSpecification : IActionSubjectSpecification
    {
        /// <summary>
        /// Возвращает true, если ground jump-on policy применима к указанному target.
        /// </summary>
        public bool IsSubjectValid(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            // Проверяет planning context и target.
            if (planningState?.Hamster == null
                || obstacle == null)
            {
                return false;
            }

            // Проверяет ground-run состояние.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.HamsterState != HamsterStateEnum.Run
                || hamster.IsOnRoof
                || hamster.IsShifting)
            {
                return false;
            }

            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            return ObstacleClassifier.CanJumpOnGroundObstacle(obstacle.ObstacleType);
        }
    }
}
