using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Проверяет, что сохранённый fire shift super jump-over всё ещё валиден.
    /// </summary>
    internal sealed class SuperJumpOverScheduledFireShiftValidator : IJumpScheduledFireShiftValidator
    {
        private readonly IJumpSearchWindowPolicy _searchWindowPolicy;
        private readonly JumpOutcomeMatcher _outcomeMatcher;

        public SuperJumpOverScheduledFireShiftValidator()
        {
            _searchWindowPolicy = new GroundJumpSearchWindowPolicy();
            _outcomeMatcher = new JumpOutcomeMatcher(
                HamsterStateEnum.SuperJumpOver,
                damageBigAliveWithoutYByReach: false,
                SuperJumpOutcomeResolver.ResolveSuperJump);
        }

        public bool IsScheduledFireShiftStillValid(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            PlannedAction action,
            float validationEpsilon)
        {
            if (planningState == null || projectedWorldSnapshot == null || targetObstacle == null || action == null)
                return false;

            // Восстанавливаем допустимое окно для уже сохранённого action.
            if (!_searchWindowPolicy.TryGetSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            // Проверяем, что оставшийся fire shift всё ещё лежит внутри окна.
            if (!JumpScheduledFireShift.TryGetRemaining(projectedWorldSnapshot, targetObstacle, action, out float fireShift))
                return false;

            if (fireShift < firstFireShift - validationEpsilon || fireShift > lastFireShift + validationEpsilon)
                return false;

            // Подтверждаем exact outcome на восстановленном fire shift.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            List<JumpObstacleData> shiftedObstacles = new(baseObstacles.Count);
            return _outcomeMatcher.IsExactOutcomeAtShift(
                planningState.Hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                action.PostFireWorldShift,
                targetObstacleIndex);
        }
    }
}