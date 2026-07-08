using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Ищет и подтверждает fire shift для прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofFireWindowFinder
    {
        /// <summary>
        /// Policy конкретного варианта roof-to-roof прыжка.
        /// </summary>
        private readonly IJumpFromRoofOnRoofPolicy _policy;
        private readonly List<JumpObstacleData> _baseObstacles = new();
        private readonly List<JumpObstacleData> _shiftedObstacles = new();

        public JumpFromRoofOnRoofFireWindowFinder(IJumpFromRoofOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Пытается найти fire shift, подтвержденный runtime roof-jump resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpFromRoofOnRoofTravel travel,
            out ObstacleSnapshot targetRoof,
            out int targetRoofIndex,
            out float firstFireShift,
            out float lastFireShift,
            out float fireShift,
            out string deadEndReason)
        {
            // Инициализирует пустой результат.
            targetRoof = null;
            targetRoofIndex = -1;
            firstFireShift = 0f;
            lastFireShift = 0f;
            fireShift = 0f;
            deadEndReason = null;

            // Находит target roof для текущего roof-to-roof сценария.
            if (!TryFindTargetRoof(
                    planningState,
                    projectedWorldSnapshot,
                    chain,
                    travel,
                    out ObstacleSnapshot lastRoof,
                    out ObstacleSnapshot runFromRoofBlocker,
                    out ObstacleSnapshot lastObstacleBeforeTargetRoof,
                    out targetRoof,
                    out targetRoofIndex))
            {
                return false;
            }

            // Вычисляет геометрическое окно запуска.
            if (!JumpFromRoofOnRoofWindowCalculator.TryCalculate(
                    planningState,
                    lastRoof,
                    targetRoof,
                    runFromRoofBlocker,
                    lastObstacleBeforeTargetRoof,
                    _policy.BigAliveCollisionPaddingRatio,
                    travel,
                    out firstFireShift,
                    out lastFireShift,
                    out fireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Подтверждает смысловые точки окна через runtime resolver.
            float selectedFireShift = fireShift;
            JumpObstacleProjection.BuildBase(projectedWorldSnapshot, _baseObstacles);
            if (TrySelectFireShift(
                    planningState,
                    projectedWorldSnapshot,
                    _baseObstacles,
                    targetRoof.InstanceId,
                    selectedFireShift,
                    firstFireShift,
                    lastFireShift,
                    travel,
                    out fireShift))
            {
                return true;
            }

            deadEndReason = "Нет безопасного окна для прыжка на следующую крышу: runtime-модель не подтверждает посадку на выбранную крышу.";
            return false;
        }

        /// <summary>
        /// Выбирает первую runtime-valid точку окна: selected, first, last.
        /// </summary>
        private bool TrySelectFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int expectedTargetRoofInstanceId,
            float selectedFireShift,
            float firstFireShift,
            float lastFireShift,
            JumpFromRoofOnRoofTravel travel,
            out float fireShift)
        {
            float[] candidateFireShifts =
            {
                selectedFireShift,
                firstFireShift,
                lastFireShift
            };

            for (int candidateIndex = 0; candidateIndex < candidateFireShifts.Length; candidateIndex++)
            {
                float candidateFireShift = candidateFireShifts[candidateIndex];
                if (!CheckRuntimeOutcomeAtFireShift(
                        planningState,
                        projectedWorldSnapshot,
                        baseObstacles,
                        expectedTargetRoofInstanceId,
                        candidateFireShift,
                        travel))
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
        /// Находит следующую roof-цель, если простой сход с крыши опасен для текущего decision point.
        /// </summary>
        internal bool TryFindTargetRoof(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpFromRoofOnRoofTravel travel,
            out ObstacleSnapshot lastRoof,
            out ObstacleSnapshot runFromRoofBlocker,
            out ObstacleSnapshot lastObstacleBeforeTargetRoof,
            out ObstacleSnapshot targetRoof,
            out int targetRoofIndex)
        {
            // Инициализирует пустой результат поиска.
            lastRoof = null;
            runFromRoofBlocker = null;
            lastObstacleBeforeTargetRoof = null;
            targetRoof = null;
            targetRoofIndex = -1;

            // Отбрасывает некорректный вход и недостающий snapshot хомяка.
            if (planningState == null || projectedWorldSnapshot == null || chain == null)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            // Находит крышу, с которой бот собирается выполнять прыжок.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out lastRoof,
                    out int lastRoofIndex))
            {
                return false;
            }

            // Roof-to-roof нужен только когда общий safety слой подтверждает опасный сход.
            if (IsPassiveRoofExitSafe(hamster, projectedWorldSnapshot, lastRoof, travel))
                return false;

            // Одним проходом подтверждает blocker для небезопасного схода и находит следующую roof-цель.
            bool hasRunFromRoofBlocker = false;
            for (int obstacleIndex = lastRoofIndex + 1;
                 obstacleIndex < projectedWorldSnapshot.Obstacles.Count;
                 obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (!IsObstacleAheadOnCurrentLane(obstacle, hamster, lastRoof))
                    continue;

                if (targetRoof == null && ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                {
                    targetRoof = obstacle;
                    targetRoofIndex = obstacleIndex;

                    if (hasRunFromRoofBlocker)
                        return true;
                }

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (targetRoof == null)
                    lastObstacleBeforeTargetRoof = obstacle;

                if (!chain.ContainsObstacle(obstacle) && !hasRunFromRoofBlocker)
                    return false;

                hasRunFromRoofBlocker = true;
                runFromRoofBlocker ??= obstacle;
                if (targetRoof != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает true, если общий passive-exit safety contract разрешает обычный сход с текущей roof.
        /// </summary>
        private static bool IsPassiveRoofExitSafe(
            HamsterSnapshot hamster,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot lastRoof,
            JumpFromRoofOnRoofTravel travel)
        {
            // Проверяет тот же runtime window, что и PassiveRoofExitStrategy.
            if (!RoofExitSafety.TryGetRunFromRoofWindow(
                    hamster,
                    lastRoof,
                    travel.RunFromRoofTravel,
                    out float runFromRoofStartShift,
                    out float runFromRoofCompletionShift))
            {
                return false;
            }

            return RoofExitSafety.IsSafeDuringRunFromRoof(
                hamster,
                projectedWorldSnapshot,
                hamster.IsOnBottomLine,
                runFromRoofStartShift,
                runFromRoofCompletionShift,
                out _);
        }

        /// <summary>
        /// Проверяет runtime outcome для указанного fire shift.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            int expectedTargetRoofInstanceId,
            float fireShift,
            JumpFromRoofOnRoofTravel travel)
        {
            // Отбрасывает вызов без обязательных данных для runtime-проверки.
            if (planningState == null || projectedWorldSnapshot == null || baseObstacles == null)
                return false;

            // Строит obstacle snapshot на момент fire.
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, _shiftedObstacles);

            // Готовит roof-jump context из текущей геометрии хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null)
                return false;

            RoofJumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                travel.RoofJumpTravel,
                travel.JumpFromRoofTravel);

            // Сверяет resolver outcome с ожидаемой посадкой на конкретную target roof.
            JumpResolveResult result = _policy.Resolve(_shiftedObstacles, context);
            if (result.State != _policy.ExpectedSuccessState)
                return false;

            if (result.TargetIndex < 0 || result.TargetIndex >= _shiftedObstacles.Count)
                return false;

            // Подтверждает совпадение target roof и в resolver snapshot, и в projected world.
            return _shiftedObstacles[result.TargetIndex].InstanceId == expectedTargetRoofInstanceId
                && result.TargetIndex < projectedWorldSnapshot.Obstacles.Count
                && projectedWorldSnapshot.Obstacles[result.TargetIndex].InstanceId == expectedTargetRoofInstanceId;
        }

        /// <summary>
        /// Возвращает true, если obstacle находится впереди на текущей линии roof-run.
        /// </summary>
        private static bool IsObstacleAheadOnCurrentLane(
            ObstacleSnapshot obstacle,
            HamsterSnapshot hamster,
            ObstacleSnapshot lastRoof)
        {
            // Проверяет наличие obstacle и линию.
            if (obstacle == null
                || obstacle.IsRemovedInPlanning
                || obstacle.IsBottomLine != hamster.IsOnBottomLine)
            {
                return false;
            }

            // Проверяет положение относительно текущей крыши.
            return obstacle.RightX > lastRoof.RightX;
        }
    }
}
