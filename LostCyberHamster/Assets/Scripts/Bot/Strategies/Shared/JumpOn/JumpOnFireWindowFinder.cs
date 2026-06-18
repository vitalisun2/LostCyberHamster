using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Подтверждает fire shift для role-based ground jump-on target.
    /// </summary>
    internal sealed class JumpOnFireWindowFinder
    {
        private readonly IJumpOnPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного jump-on policy.
        /// </summary>
        public JumpOnFireWindowFinder(IJumpOnPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Находит fire shift, который попадает в target по runtime resolver-у и безопасен после возврата в Run.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            JumpOnTravel travel,
            out JumpOnWindowModel window,
            out float fireShift,
            out string deadEndReason)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)),
                (targetObstacle, nameof(targetObstacle)));

            // Вычисляет аналитическое окно.
            fireShift = 0f;
            deadEndReason = null;
            if (!JumpOnWindowCalculator.TryCalculate(
                    planningState.Hamster,
                    chain,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    travel,
                    out window,
                    out deadEndReason))
            {
                return false;
            }

            // Подтверждает смысловые точки окна через runtime resolver и post-action safety.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            if (TrySelectFireShift(
                    planningState,
                    projectedWorldSnapshot,
                    baseObstacles,
                    travel,
                    window,
                    out fireShift,
                    out deadEndReason))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Выбирает первую подходящую смысловую точку окна: middle, first, last.
        /// </summary>
        private bool TrySelectFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            JumpOnTravel travel,
            JumpOnWindowModel window,
            out float fireShift,
            out string deadEndReason)
        {
            // Сохраняет прежний preferred timing, но не отбрасывает всё окно из-за одной точки.
            bool hasRuntimeValidCandidate = false;
            string postActionDeadEndReason = null;
            float[] candidateFireShifts =
            {
                window.SelectedFireShift,
                window.FirstFireShift,
                window.LastFireShift
            };

            for (int candidateIndex = 0; candidateIndex < candidateFireShifts.Length; candidateIndex++)
            {
                float candidateFireShift = candidateFireShifts[candidateIndex];
                if (!CheckRuntimeOutcomeAtFireShift(
                        planningState.Hamster,
                        baseObstacles,
                        candidateFireShift,
                        travel,
                        window.TargetObstacleIndex))
                {
                    continue;
                }

                hasRuntimeValidCandidate = true;
                float completionWorldShift = candidateFireShift + travel.ActionTravel;
                if (!TargetRemovalPostActionSafety.IsSafeAfterCompletion(
                        planningState,
                        projectedWorldSnapshot,
                        window.TargetObstacleIndex,
                        window.TargetObstacle.InstanceId,
                        completionWorldShift,
                        out postActionDeadEndReason))
                {
                    continue;
                }

                fireShift = candidateFireShift;
                deadEndReason = null;
                return true;
            }

            fireShift = 0f;
            deadEndReason = hasRuntimeValidCandidate
                ? postActionDeadEndReason
                : "Нет безопасного окна для напрыгивания: runtime-модель не подтверждает попадание в target.";
            return false;
        }

        /// <summary>
        /// Проверяет, что runtime resolver в заданный fire shift попадает в ожидаемый target.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnTravel travel,
            int targetObstacleIndex)
        {
            // Отсекает невалидные входные данные.
            if (hamster == null
                || baseObstacles == null
                || fireShift < 0f
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                travel);

            return result.State == _policy.ExpectedJumpOnState
                   && result.TargetIndex == targetObstacleIndex;
        }

        /// <summary>
        /// Проецирует мир в момент fire shift и вызывает runtime resolver.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnTravel travel)
        {
            // Отсекает невозможную дистанцию resolver-а.
            if (travel.ResolveTravel <= 0f)
                return new JumpResolveResult(hamster.HamsterState, -1);

            // Сдвигает obstacles в позицию runtime resolver-а.
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(
                baseObstacles,
                travel.GetResolveFireShift(fireShift),
                obstaclesAtFireShift);

            // Собирает runtime-equivalent context resolver-а.
            JumpResolveContext context = CreateResolveContext(hamster, travel);

            return _policy.Resolve(obstaclesAtFireShift, context);
        }

        /// <summary>
        /// Создает context resolver-а с policy-specific mid-Y проверкой.
        /// </summary>
        private JumpResolveContext CreateResolveContext(
            HamsterSnapshot hamster,
            JumpOnTravel travel)
        {
            if (_policy.TryGetJumpMidYShift(out float jumpMidYShift))
            {
                float hamsterCenterY = (hamster.HamsterBottomY + hamster.HamsterTopY) * 0.5f;
                float hamsterHalfHeight = hamster.Height * 0.5f;
                float hamsterJumpMidCenterY = hamsterCenterY + jumpMidYShift;
                return new JumpResolveContext(
                    hamster.IsOnBottomLine,
                    hamster.HamsterLeftX,
                    hamster.HamsterRightX,
                    hamster.CenterX,
                    hamster.Width,
                    travel.ResolveTravel,
                    travel.ResolveTravel,
                    hasJumpMidY: true,
                    hamsterJumpMidBottomY: hamsterJumpMidCenterY - hamsterHalfHeight,
                    hamsterJumpMidTopY: hamsterJumpMidCenterY + hamsterHalfHeight);
            }

            return new JumpResolveContext(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.ResolveTravel,
                travel.ResolveTravel,
                damageBigAliveWithoutYByReach: false);
        }
    }
}
