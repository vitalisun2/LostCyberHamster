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
            out float fireShift)
        {
            // Инициализирует пустой результат.
            targetRoof = null;
            targetRoofIndex = -1;
            firstFireShift = 0f;
            lastFireShift = 0f;
            fireShift = 0f;

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
                    out fireShift))
            {
                return false;
            }

            // Подтверждает выбранную точку через runtime resolver.
            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            return CheckRuntimeOutcomeAtFireShift(
                planningState,
                projectedWorldSnapshot,
                baseObstacles,
                targetRoof.InstanceId,
                fireShift,
                travel);
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

            // Одним проходом подтверждает blocker для схода с крыши и находит следующую roof-цель.
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

                float gap = obstacle.LeftX - lastRoof.RightX;
                if (gap >= travel.RunFromRoofTravel && !hasRunFromRoofBlocker)
                    return false;

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
            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, obstaclesAtFireShift);

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
            JumpResolveResult result = _policy.Resolve(obstaclesAtFireShift, context);
            if (result.State != _policy.ExpectedSuccessState)
                return false;

            if (result.TargetIndex < 0 || result.TargetIndex >= obstaclesAtFireShift.Count)
                return false;

            // Подтверждает совпадение target roof и в resolver snapshot, и в projected world.
            return obstaclesAtFireShift[result.TargetIndex].InstanceId == expectedTargetRoofInstanceId
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
            if (obstacle == null || obstacle.IsBottomLine != hamster.IsOnBottomLine)
                return false;

            // Проверяет положение относительно текущей крыши.
            return obstacle.RightX > lastRoof.RightX;
        }
    }
}
