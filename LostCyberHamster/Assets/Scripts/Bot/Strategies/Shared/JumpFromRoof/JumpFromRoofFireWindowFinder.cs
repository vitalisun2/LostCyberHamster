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
        private readonly List<JumpObstacleData> _baseObstacles = new();
        private readonly List<JumpObstacleData> _shiftedObstacles = new();

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
            out float fireShift,
            out string deadEndReason)
        {
            // Вычисляет covered chain и допустимое окно.
            fireShift = 0f;
            deadEndReason = null;
            if (!JumpFromRoofChainCalculator.TryCalculate(
                    _policy,
                    planningState,
                    chain,
                    lastRoof,
                    travel,
                    out chainModel,
                    out deadEndReason))
            {
                return false;
            }

            // Подтверждает смысловые точки окна через runtime resolver.
            JumpObstacleProjection.BuildBase(projectedWorldSnapshot, _baseObstacles);
            if (TrySelectFireShift(
                    planningState,
                    projectedWorldSnapshot,
                    _baseObstacles,
                    chainModel,
                    travel,
                    out fireShift,
                    out deadEndReason))
            {
                return true;
            }

            if (deadEndReason == null)
                deadEndReason = "Нет безопасного окна для прыжка с крыши: runtime-модель не подтверждает безопасный результат прыжка.";
            return false;
        }

        /// <summary>
        /// Выбирает первую точку окна, которая проходит runtime outcome и post-action Run re-entry safety.
        /// </summary>
        private bool TrySelectFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            JumpFromRoofChainModel chainModel,
            JumpFromRoofTravel travel,
            out float fireShift,
            out string deadEndReason)
        {
            bool hasRuntimeValidCandidate = false;
            string postActionDeadEndReason = null;
            float[] candidateFireShifts =
            {
                chainModel.SelectedFireShift,
                chainModel.FirstFireShift,
                chainModel.LastFireShift
            };

            for (int candidateIndex = 0; candidateIndex < candidateFireShifts.Length; candidateIndex++)
            {
                float candidateFireShift = candidateFireShifts[candidateIndex];
                if (!CheckRuntimeOutcomeAtFireShift(
                        planningState,
                        baseObstacles,
                        candidateFireShift,
                        travel))
                {
                    continue;
                }

                hasRuntimeValidCandidate = true;
                float completionWorldShift = candidateFireShift + travel.ActionTravel;
                if (!RoofExitSafety.IsSafeAfterRunReentry(
                        planningState.Hamster,
                        projectedWorldSnapshot,
                        planningState.Hamster.IsOnBottomLine,
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
                : null;
            return false;
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
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, _shiftedObstacles);

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

            JumpResolveResult result = _policy.Resolve(_shiftedObstacles, context);
            return result.State == _policy.ExpectedSuccessState;
        }
    }
}
