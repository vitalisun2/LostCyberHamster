using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Подбирает и подтверждает fire shift для role-based roof-to-road jump-on target.
    /// </summary>
    internal sealed class JumpOnFromRoofFireWindowFinder
    {
        /// <summary>
        /// Минимальное различие fire shift, при котором timing-кандидаты считаются разными.
        /// </summary>
        private const float FireShiftEqualityEpsilon = 0.001f;

        /// <summary>
        /// Policy конкретного варианта roof-to-road jump-on.
        /// </summary>
        private readonly IJumpOnFromRoofPolicy _policy;

        public JumpOnFromRoofFireWindowFinder(IJumpOnFromRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Находит fire shift, который попадает в аналитическое окно, подтверждается resolver-ом и безопасен после возврата в Run.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpOnFromRoofTravel travel,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            ObstacleSnapshot lastRoof,
            out JumpOnFromRoofWindowModel window,
            out float fireShift,
            out string deadEndReason)
        {
            // Собирает timing-кандидаты, подтвержденные runtime resolver-ом.
            deadEndReason = null;
            if (TryCollectFireShifts(
                    planningState,
                    projectedWorldSnapshot,
                    chain,
                    travel,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    lastRoof,
                    out window,
                    out IReadOnlyList<float> fireShifts,
                    out deadEndReason))
            {
                // Выбирает первый candidate, который безопасен после полного действия.
                string postActionDeadEndReason = null;
                for (int shiftIndex = 0; shiftIndex < fireShifts.Count; shiftIndex++)
                {
                    float candidateFireShift = fireShifts[shiftIndex];
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
                    return true;
                }

                deadEndReason = FormatRoofToRoadPostActionReason(postActionDeadEndReason);
            }

            fireShift = 0f;
            return false;
        }

        /// <summary>
        /// Собирает runtime-valid fire shifts для selected и late точек roof-to-road окна.
        /// </summary>
        public bool TryCollectFireShifts(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpOnFromRoofTravel travel,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            ObstacleSnapshot lastRoof,
            out JumpOnFromRoofWindowModel window,
            out IReadOnlyList<float> fireShifts,
            out string deadEndReason)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)),
                (targetObstacle, nameof(targetObstacle)),
                (lastRoof, nameof(lastRoof)));

            // Вычисляет аналитическое окно.
            fireShifts = null;
            deadEndReason = null;
            if (!JumpOnFromRoofWindowCalculator.TryCalculate(
                    planningState.Hamster,
                    chain,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    lastRoof,
                    travel,
                    out window,
                    out deadEndReason))
            {
                return false;
            }

            // Подтверждает смысловые timing-точки через runtime resolver.
            var validFireShifts = new List<float>();
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            TryAddRuntimeValidFireShift(
                planningState,
                baseObstacles,
                travel,
                window,
                window.SelectedFireShift,
                validFireShifts);
            TryAddRuntimeValidFireShift(
                planningState,
                baseObstacles,
                travel,
                window,
                window.LastFireShift,
                validFireShifts);

            if (validFireShifts.Count == 0)
            {
                deadEndReason = "Нет безопасного окна для напрыгивания с крыши: runtime-модель не подтверждает попадание в target.";
                return false;
            }

            fireShifts = validFireShifts;
            return true;
        }

        /// <summary>
        /// Добавляет fire shift, если он отличается от уже добавленных и попадает в target по runtime resolver.
        /// </summary>
        private void TryAddRuntimeValidFireShift(
            PlanningState planningState,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            JumpOnFromRoofTravel travel,
            JumpOnFromRoofWindowModel window,
            float fireShift,
            List<float> fireShifts)
        {
            // Отсекает дубликаты timing-точек.
            for (int shiftIndex = 0; shiftIndex < fireShifts.Count; shiftIndex++)
            {
                if (Math.Abs(fireShifts[shiftIndex] - fireShift) <= FireShiftEqualityEpsilon)
                    return;
            }

            // Подтверждает runtime outcome.
            if (!CheckRuntimeOutcomeAtFireShift(
                    planningState.Hamster,
                    baseObstacles,
                    fireShift,
                    travel,
                    window.TargetObstacleIndex,
                    window.TargetObstacle.InstanceId))
            {
                return;
            }

            fireShifts.Add(fireShift);
        }

        /// <summary>
        /// Адаптирует post-action reason для roof-to-road jump-on.
        /// </summary>
        private static string FormatRoofToRoadPostActionReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return "Небезопасное состояние после напрыгивания с крыши: после возврата в Run хомяк пересекает опасное препятствие.";

            return reason.Replace("после напрыгивания:", "после напрыгивания с крыши:");
        }

        /// <summary>
        /// Проверяет, что runtime resolver в заданный fire shift попадает в ожидаемый target.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnFromRoofTravel travel,
            int targetObstacleIndex,
            int targetObstacleInstanceId)
        {
            // Отсекает невалидные входные данные.
            if (hamster == null
                || baseObstacles == null
                || fireShift < 0f
                || travel.RoofJumpTravel <= 0f
                || travel.ResolveTravel <= 0f)
            {
                return false;
            }

            // Получает outcome runtime resolver-а.
            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                travel);

            // Сравнивает outcome с ожидаемым target.
            return result.State == _policy.ExpectedJumpOnState
                && result.TargetIndex == targetObstacleIndex
                && result.TargetIndex >= 0
                && result.TargetIndex < baseObstacles.Count
                && baseObstacles[result.TargetIndex].InstanceId == targetObstacleInstanceId;
        }

        /// <summary>
        /// Проецирует мир в момент resolver-а и вызывает runtime roof-jump resolver.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            JumpOnFromRoofTravel travel)
        {
            // Сдвигает obstacles в позицию runtime resolver-а.
            var obstaclesAtResolveShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(
                baseObstacles,
                travel.GetResolveFireShift(fireShift),
                obstaclesAtResolveShift);

            // Собирает context resolver-а относительно resolver-точки.
            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.RoofJumpTravel,
                travel.ResolveTravel);

            // Возвращает policy-specific outcome.
            return _policy.Resolve(obstaclesAtResolveShift, context);
        }
    }
}
