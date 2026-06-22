using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof
{
    /// <summary>
    /// Проверяет применимость прыжка с крыши к выбранной road threat.
    /// </summary>
    internal sealed class JumpFromRoofSpecification : IActionSubjectSpecification
    {
        /// <summary>
        /// Возвращает true, если хомяк может выполнить прыжок с крыши перед obstacle.
        /// </summary>
        public bool IsSubjectValid(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            // Проверяет context и выбранную threat.
            if (planningState?.Hamster == null || obstacle == null)
                return false;

            // Проверяет roof-run состояние.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster.HamsterState != HamsterStateEnum.RoofRun
                || !hamster.IsOnRoof
                || !hamster.RoofSupportInstanceId.HasValue
                || hamster.IsShifting)
            {
                return false;
            }

            // Проверяет, что obstacle является дорожной угрозой на текущей линии.
            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                return false;

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }
    }
}
