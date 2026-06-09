using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof
{
    /// <summary>
    /// Подбирает fire shift для role-based прыжка с крыши через road threats.
    /// </summary>
    internal sealed class JumpFromRoofFireWindowFinder
    {
        private readonly IJumpFromRoofPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного варианта прыжка с крыши.
        /// </summary>
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
            // Вычисляет covered chain и допустимое окно.
            fireShift = 0f;
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

            // Подтверждает выбранную точку через runtime resolver.
            fireShift = chainModel.SelectedFireShift;
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState,
                baseObstacles,
                fireShift,
                travel);
        }

        /// <summary>
        /// Проверяет runtime outcome для заданного fire shift.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpFromRoofTravel travel)
        {
            if (planningState?.Hamster == null
                || baseObstacles == null
                || fireShift < 0f
                || travel.RoofJumpTravel <= 0f
                || travel.ActionTravel <= 0f)
            {
                return false;
            }

            // Строит snapshot препятствий на момент fire.
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

            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            return result.State == _policy.ExpectedSuccessState;
        }
    }
}
