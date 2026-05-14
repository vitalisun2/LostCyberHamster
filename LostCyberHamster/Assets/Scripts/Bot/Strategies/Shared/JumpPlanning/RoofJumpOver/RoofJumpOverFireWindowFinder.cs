using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Подбирает fire shift для roof jump-over над chain препятствий на крыше.
    /// </summary>
    internal sealed class RoofJumpOverFireWindowFinder
    {
        private readonly IRoofJumpOverPolicy _policy;

        public RoofJumpOverFireWindowFinder(IRoofJumpOverPolicy policy)
        {
            _policy = policy;
        }

        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            RoofJumpOverTravel travel,
            out RoofJumpOverChainModel chainModel,
            out ObstacleSnapshot resultSupportObstacle,
            out float fireShift)
        {
            resultSupportObstacle = null;
            fireShift = 0f;

            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Вычисляет chain и допустимое окно запуска.
            if (!RoofJumpOverChainCalculator.TryCalculate(
                    planningState,
                    projectedWorldSnapshot,
                    chain,
                    travel,
                    out chainModel))
            {
                return false;
            }

            // Проверяет выбранный fire shift через runtime resolver.
            fireShift = chainModel.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return TryGetRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                fireShift,
                travel,
                out resultSupportObstacle);
        }

        internal bool CheckRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int expectedSupportInstanceId,
            float fireShift,
            RoofJumpOverTravel travel)
        {
            return TryGetRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                fireShift,
                travel,
                out ObstacleSnapshot resultSupportObstacle)
                   && resultSupportObstacle.InstanceId == expectedSupportInstanceId;
        }

        private bool TryGetRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            RoofJumpOverTravel travel,
            out ObstacleSnapshot resultSupportObstacle)
        {
            resultSupportObstacle = null;

            // Строит obstacle snapshot на момент fire.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Готовит roof-jump context из текущей геометрии хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.RoofJumpTravel,
                travel.JumpFromRoofTravel);

            // Сверяет resolver outcome с ожидаемым продолжением RoofRun.
            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            if (result.State != _policy.ExpectedSuccessState)
                return false;

            if (result.TargetIndex < 0 || result.TargetIndex >= projectedWorldSnapshot.Obstacles.Count)
                return false;

            resultSupportObstacle = projectedWorldSnapshot.Obstacles[result.TargetIndex];
            if (resultSupportObstacle.InstanceId != obstaclesAtFireShift[result.TargetIndex].InstanceId)
                return false;

            // Подтверждает, что target resolver-а остаётся валидной roof support-платформой.
            return RoofRunProjection.IsPassiveRoofContinuation(
                planningState,
                projectedWorldSnapshot,
                resultSupportObstacle);
        }
    }
}