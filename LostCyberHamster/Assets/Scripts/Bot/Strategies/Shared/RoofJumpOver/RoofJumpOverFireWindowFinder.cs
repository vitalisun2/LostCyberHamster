using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Подбирает fire shift для roof jump-over и подтверждает runtime roof support.
    /// </summary>
    internal sealed class RoofJumpOverFireWindowFinder
    {
        /// <summary>
        /// Policy конкретного варианта roof jump-over.
        /// </summary>
        private readonly IRoofJumpOverPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного варианта roof jump-over.
        /// </summary>
        public RoofJumpOverFireWindowFinder(IRoofJumpOverPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift и result support, подтвержденные runtime resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            RoofJumpOverTravel travel,
            out RoofJumpOverChainModel chainModel,
            out ObstacleSnapshot resultSupportObstacle,
            out float fireShift,
            out string deadEndReason)
        {
            // Проверяет обязательные входные данные.
            resultSupportObstacle = null;
            fireShift = 0f;
            deadEndReason = null;
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)));

            // Вычисляет covered chain и допустимое окно запуска.
            if (!RoofJumpOverChainCalculator.TryCalculate(
                    planningState,
                    projectedWorldSnapshot,
                    chain,
                    travel,
                    out chainModel,
                    out deadEndReason))
            {
                return false;
            }

            // Проверяет смысловые точки fire-window через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return TrySelectFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                chainModel,
                travel,
                out resultSupportObstacle,
                out fireShift,
                out deadEndReason);
        }

        /// <summary>
        /// Выбирает первую runtime-valid точку окна: selected, first, last.
        /// </summary>
        private bool TrySelectFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            RoofJumpOverChainModel chainModel,
            RoofJumpOverTravel travel,
            out ObstacleSnapshot resultSupportObstacle,
            out float fireShift,
            out string deadEndReason)
        {
            resultSupportObstacle = null;
            deadEndReason = null;
            float[] candidateFireShifts =
            {
                chainModel.SelectedFireShift,
                chainModel.FirstFireShift,
                chainModel.LastFireShift
            };

            for (int candidateIndex = 0; candidateIndex < candidateFireShifts.Length; candidateIndex++)
            {
                float candidateFireShift = candidateFireShifts[candidateIndex];
                if (!TryGetRuntimeOutcomeAtFireShift(
                        planningState,
                        projectedWorldSnapshot,
                        baseObstacles,
                        candidateFireShift,
                        travel,
                        out resultSupportObstacle,
                        out deadEndReason))
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
                out ObstacleSnapshot resultSupportObstacle,
                out _)
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
            out ObstacleSnapshot resultSupportObstacle,
            out string deadEndReason)
        {
            // Проверяет вход и строит obstacles на момент fire.
            resultSupportObstacle = null;
            deadEndReason = null;
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
            {
                deadEndReason = "Нет безопасного окна для прыжка над препятствием на крыше: runtime-модель не подтверждает безопасный прыжок.";
                return false;
            }

            if (result.TargetIndex < 0 || result.TargetIndex >= projectedWorldSnapshot.Obstacles.Count)
            {
                deadEndReason = "Нет безопасного окна для прыжка над препятствием на крыше: runtime-модель не подтверждает безопасный прыжок.";
                return false;
            }

            resultSupportObstacle = projectedWorldSnapshot.Obstacles[result.TargetIndex];
            if (resultSupportObstacle.InstanceId != obstaclesAtFireShift[result.TargetIndex].InstanceId)
            {
                deadEndReason = "Нет безопасного окна для прыжка над препятствием на крыше: runtime-модель не подтверждает безопасный прыжок.";
                return false;
            }

            if (RoofRunProjection.IsPassiveRoofContinuation(
                planningState,
                projectedWorldSnapshot,
                resultSupportObstacle))
            {
                return true;
            }

            deadEndReason = "Небезопасное состояние после прыжка по крыше: после приземления нет безопасного продолжения RoofRun.";
            return false;
        }
    }
}
