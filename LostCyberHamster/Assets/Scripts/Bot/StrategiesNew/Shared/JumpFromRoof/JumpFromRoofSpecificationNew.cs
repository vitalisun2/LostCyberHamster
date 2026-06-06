using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.StrategiesNew.Shared.Contracts;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoof
{
    /// <summary>
    /// Проверяет применимость прыжка с крыши к выбранной road threat.
    /// </summary>
    internal sealed class JumpFromRoofSpecificationNew : IBotStrategySpecification
    {
        private readonly IJumpFromRoofPolicy _policy;

        /// <summary>
        /// Создает specification для конкретного варианта прыжка с крыши.
        /// </summary>
        public JumpFromRoofSpecificationNew(IJumpFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Возвращает true, если хомяк может выполнить прыжок с крыши перед obstacle.
        /// </summary>
        public bool IsSatisfiedBy(
            PlanningState planningState,
            ObstacleSnapshot obstacle)
        {
            // Проверяет context и выбранную threat.
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

            // Проверяет, что obstacle является дорожной угрозой на текущей линии.
            if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                return false;

            return ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }
    }
}
