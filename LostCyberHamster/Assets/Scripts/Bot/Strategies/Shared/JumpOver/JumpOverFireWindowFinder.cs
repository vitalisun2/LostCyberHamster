using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOver
{
    /// <summary>
    /// Подбирает fire shift для role-based ground jump-over chain.
    /// </summary>
    internal sealed class JumpOverFireWindowFinder
    {
        private readonly IJumpOverPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного jump-over policy.
        /// </summary>
        public JumpOverFireWindowFinder(IJumpOverPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift и подтвердить его runtime-equivalent resolver'ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            float jumpTravel,
            out JumpOverChainModel chainWindow,
            out float fireShift,
            out string deadEndReason)
        {
            // Проверяет входы и вычисляет геометрическое окно.
            chainWindow = default;
            fireShift = 0f;
            deadEndReason = null;
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            if (!JumpOverChainCalculator.TryCalculate(
                    _policy,
                    planningState.Hamster,
                    chain,
                    jumpTravel,
                    out chainWindow,
                    out deadEndReason))
            {
                return false;
            }

            // Проверяет смысловые точки fire-window через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            if (TrySelectFireShift(
                    planningState.Hamster,
                    baseObstacles,
                    jumpTravel,
                    chainWindow,
                    out fireShift))
            {
                return true;
            }

            deadEndReason = "Нет безопасного окна для перепрыгивания: runtime-модель не подтверждает безопасный перелет выбранной цепочки.";
            return false;
        }

        /// <summary>
        /// Выбирает первую runtime-valid точку окна: selected, first, last.
        /// </summary>
        private bool TrySelectFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float jumpTravel,
            JumpOverChainModel chainWindow,
            out float fireShift)
        {
            float[] candidateFireShifts =
            {
                chainWindow.SelectedFireShift,
                chainWindow.FirstFireShift,
                chainWindow.LastFireShift
            };

            for (int candidateIndex = 0; candidateIndex < candidateFireShifts.Length; candidateIndex++)
            {
                float candidateFireShift = candidateFireShifts[candidateIndex];
                if (!CheckRuntimeOutcomeAtFireShift(
                        hamster,
                        baseObstacles,
                        candidateFireShift,
                        jumpTravel,
                        chainWindow))
                {
                    continue;
                }

                fireShift = candidateFireShift;
                return true;
            }

            fireShift = 0f;
            return false;
        }

        /// <summary>
        /// Проверяет, что runtime resolver завершит прыжок ожидаемым over-состоянием.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            JumpOverChainModel chainWindow)
        {
            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);
            return result.State == _policy.ExpectedOverState
                   && chainWindow.IsLastObstacle(result.TargetIndex);
        }

        /// <summary>
        /// Возвращает runtime-equivalent outcome для выбранного fire shift.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel)
        {
            // Проецирует obstacle snapshot к моменту запуска.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

            // Делегирует финальную проверку runtime-equivalent resolver'у policy.
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                jumpTravel,
                jumpTravel,
                damageBigAliveWithoutYByReach: _policy.DamageBigAliveWithoutYByReach);

            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            return result;
        }
    }
}
