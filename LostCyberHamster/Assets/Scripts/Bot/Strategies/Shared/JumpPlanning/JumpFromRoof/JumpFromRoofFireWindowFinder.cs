using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Подбирает fire shift для прыжка с крыши через дорожную obstacle chain.
    /// </summary>
    internal sealed class JumpFromRoofFireWindowFinder
    {
        /// <summary>
        /// Хранит runtime-отличия конкретного варианта прыжка с крыши.
        /// </summary>
        private readonly IJumpFromRoofPolicy _policy;

        public JumpFromRoofFireWindowFinder(IJumpFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift, который runtime resolver подтверждает как успешный прыжок с крыши.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            ObstacleSnapshot lastRoof,
            JumpFromRoofTravel travel,
            out JumpFromRoofChainModel chainModel,
            out float fireShift)
        {
            // Инициализирует пустой результат.
            fireShift = 0f;

            // Вычисляет chain и допустимое окно запуска.
            if (!JumpFromRoofChainCalculator.TryCalculate(
                    _policy,
                    planningState,
                    chain,
                    lastRoof,
                    travel,
                    out chainModel))
            {
                return false;
            }

            // Проверяет выбранный fire shift через runtime resolver.
            fireShift = chainModel.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState,
                baseObstacles,
                fireShift,
                travel);
        }

        /// <summary>
        /// Проверяет runtime outcome для указанного fire shift.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpFromRoofTravel travel)
        {
            // Строит obstacle snapshot на момент fire.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Готовит roof-jump context.
            HamsterSnapshot hamster = planningState.Hamster;
            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.RoofJumpTravel,
                travel.ActionTravel);

            // Сверяет resolver outcome с ожидаемым прыжком с крыши.
            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            return result.State == _policy.ExpectedSuccessState;
        }
    }
}
