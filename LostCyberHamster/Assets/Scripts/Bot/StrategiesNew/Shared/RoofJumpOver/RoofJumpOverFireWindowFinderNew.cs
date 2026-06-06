using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.RoofJumpOver
{
    /// <summary>
    /// Подбирает fire shift для roof jump-over и подтверждает runtime roof support.
    /// </summary>
    internal sealed class RoofJumpOverFireWindowFinderNew
    {
        /// <summary>
        /// Policy конкретного варианта roof jump-over.
        /// </summary>
        private readonly IRoofJumpOverPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного варианта roof jump-over.
        /// </summary>
        public RoofJumpOverFireWindowFinderNew(IRoofJumpOverPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift и result support, подтвержденные runtime resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChainNew chain,
            RoofJumpOverTravel travel,
            out RoofJumpOverChainModel chainModel,
            out ObstacleSnapshot resultSupportObstacle,
            out float fireShift)
        {
            // Проверяет обязательные входные данные.
            resultSupportObstacle = null;
            fireShift = 0f;
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Вычисляет covered chain и допустимое окно запуска.
            if (!RoofJumpOverChainCalculatorNew.TryCalculate(
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

        /// <summary>
        /// Проверяет, что retained fire shift всё ещё возвращает тот же roof support.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int expectedSupportInstanceId,
            float fireShift,
            RoofJumpOverTravel travel)
        {
            // Получает runtime outcome для сохраненного fire shift.
            return TryGetRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                fireShift,
                travel,
                out ObstacleSnapshot resultSupportObstacle)
                   && resultSupportObstacle.InstanceId == expectedSupportInstanceId;
        }

        /// <summary>
        /// Получает runtime outcome и проверяет continuation support.
        /// </summary>
        private bool TryGetRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            RoofJumpOverTravel travel,
            out ObstacleSnapshot resultSupportObstacle)
        {
            // Проверяет вход и строит obstacles на момент fire.
            resultSupportObstacle = null;
            if (planningState?.Hamster == null
                || projectedWorldSnapshot?.Obstacles == null
                || baseObstacles == null
                || fireShift < 0f
                || travel.RoofJumpTravel <= 0f
                || travel.JumpFromRoofTravel <= 0f)
            {
                return false;
            }

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

            return RoofRunProjection.IsPassiveRoofContinuation(
                planningState,
                projectedWorldSnapshot,
                resultSupportObstacle);
        }
    }
}
