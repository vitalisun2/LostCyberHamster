using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.FireWindows;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Ищет fire moment для super jump-over.
    /// </summary>
    internal sealed class SuperJumpOverFireWindowFinder
    {
        private const float _searchStep = 0.005f;
        private const float _searchEpsilon = 0.0001f;
        private const float _distanceFromIntervalEnd = 0.1f;
        private const float _firePositionInInterval = 1f;

        #region Public API

        /// <summary>
        /// Подбирает момент fire внутри допустимого окна для super jump-over.
        /// </summary>
        public bool TryFindFireMoment(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float fireMoment)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (targetObstacle, nameof(targetObstacle)));

            if (!TryGetFireWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    superJumpTravel,
                    out FireWindow fireWindow))
            {
                fireMoment = 0f;
                return false;
            }

            List<JumpObstacleData> baseObstacles = BuildBaseObstacles(projectedWorldSnapshot);
            List<FireInterval> successfulIntervals = FireWindowScanner.FindSuccessfulIntervals(
                fireWindow,
                _searchStep,
                _searchEpsilon,
                candidateFireMoment => IsExpectedOutcomeAtFireMoment(
                    planningState.Hamster,
                    baseObstacles,
                    candidateFireMoment,
                    superJumpTravel,
                    targetObstacleIndex));

            return TrySelectFireMoment(successfulIntervals, out fireMoment);
        }

        #endregion

        #region Search Window

        /// <summary>
        /// Получает физически допустимое окно запуска super jump-over.
        /// </summary>
        private static bool TryGetFireWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out FireWindow fireWindow)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            float chainRightX = GetRoadSmallChainRightX(
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex);

            float firstFireShift = chainRightX - hamster.HamsterLeftX - superJumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            float lastFireShift = targetObstacle.LeftX - hamster.HamsterRightX;
            fireWindow = new FireWindow(firstFireShift, lastFireShift);
            return lastFireShift >= 0f && firstFireShift <= lastFireShift;
        }

        /// <summary>
        /// Возвращает правую границу target obstacle или всей цепочки road small obstacles.
        /// </summary>
        private static float GetRoadSmallChainRightX(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex)
        {
            float chainRightX = targetObstacle.RightX;
            if (projectedWorldSnapshot == null
                || targetObstacleIndex < 0
                || targetObstacleIndex >= projectedWorldSnapshot.Obstacles.Count
                || !ObstacleClassifier.IsRoadSmallOverChainObstacle(targetObstacle.ObstacleType))
            {
                return chainRightX;
            }

            bool isBottomLine = targetObstacle.IsBottomLine;
            for (int obstacleIndex = targetObstacleIndex + 1; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(obstacle.ObstacleType))
                    break;

                chainRightX = obstacle.RightX;
            }

            return chainRightX;
        }

        #endregion

        #region Obstacle Projection

        /// <summary>
        /// Преобразует planning obstacles в immutable base данные runtime resolver'а.
        /// </summary>
        private static List<JumpObstacleData> BuildBaseObstacles(WorldSnapshot projectedWorldSnapshot)
        {
            var obstacles = new List<JumpObstacleData>(projectedWorldSnapshot.Obstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                obstacles.Add(new JumpObstacleData(
                    obstacle.ObstacleType,
                    obstacle.IsBottomLine,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX));
            }

            return obstacles;
        }

        /// <summary>
        /// Строит obstacles в координатах fire moment.
        /// </summary>
        private static List<JumpObstacleData> BuildShiftedObstacles(
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireMoment)
        {
            var shiftedObstacles = new List<JumpObstacleData>(baseObstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < baseObstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = baseObstacles[obstacleIndex];
                shiftedObstacles.Add(new JumpObstacleData(
                    obstacle.Type,
                    obstacle.IsBottomLine,
                    obstacle.LeftX - fireMoment,
                    obstacle.RightX - fireMoment,
                    obstacle.CenterX - fireMoment,
                    obstacle.HasY,
                    obstacle.BottomY,
                    obstacle.TopY));
            }

            return shiftedObstacles;
        }

        #endregion

        #region Fire Moment Selection

        /// <summary>
        /// Выбирает поздний fire moment внутри последнего успешного интервала.
        /// </summary>
        private static bool TrySelectFireMoment(
            IReadOnlyList<FireInterval> successfulIntervals,
            out float fireMoment)
        {
            for (int intervalIndex = successfulIntervals.Count - 1; intervalIndex >= 0; intervalIndex--)
            {
                FireInterval interval = successfulIntervals[intervalIndex];
                if (interval.TrySelectPoint(
                        _firePositionInInterval,
                        _distanceFromIntervalEnd,
                        _searchEpsilon,
                        out fireMoment))
                {
                    return true;
                }
            }

            fireMoment = 0f;
            return false;
        }

        #endregion

        #region Outcome Resolution

        /// <summary>
        /// Проверяет, что fire moment приводит к ожидаемому outcome по ожидаемому obstacle.
        /// </summary>
        private static bool IsExpectedOutcomeAtFireMoment(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireMoment,
            float superJumpTravel,
            int targetObstacleIndex)
        {
            List<JumpObstacleData> obstaclesAtFireMoment = BuildShiftedObstacles(baseObstacles, fireMoment);
            JumpResolveResult result = GetRuntimeOutcome(
                hamster,
                obstaclesAtFireMoment,
                superJumpTravel);

            return result.State == HamsterStateEnum.SuperJumpOver
                   && IsExpectedTarget(obstaclesAtFireMoment, targetObstacleIndex, result.TargetIndex);
        }

        /// <summary>
        /// Возвращает результат runtime resolver'а для obstacles в момент fire.
        /// </summary>
        private static JumpResolveResult GetRuntimeOutcome(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> obstaclesAtFireMoment,
            float superJumpTravel)
        {
            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                superJumpTravel,
                superJumpTravel,
                damageBigAliveWithoutYByReach: false);

            return SuperJumpOutcomeResolver.ResolveSuperJump(obstaclesAtFireMoment, context);
        }

        #endregion

        #region Target Matching

        /// <summary>
        /// Проверяет прямое попадание в target или допустимый over-result по цепочке road small obstacles.
        /// </summary>
        private static bool IsExpectedTarget(
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
            int targetObstacleIndex,
            int resolvedTargetIndex)
        {
            return resolvedTargetIndex == targetObstacleIndex
                   || IsSameRoadSmallChainTarget(shiftedObstacles, targetObstacleIndex, resolvedTargetIndex);
        }

        /// <summary>
        /// Разрешает случай, когда resolver возвращает более поздний obstacle из одной цепочки road small obstacles.
        /// </summary>
        private static bool IsSameRoadSmallChainTarget(
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
            int targetObstacleIndex,
            int resolvedTargetIndex)
        {
            if (shiftedObstacles == null)
                return false;

            if (targetObstacleIndex < 0
                || resolvedTargetIndex < targetObstacleIndex
                || resolvedTargetIndex >= shiftedObstacles.Count)
            {
                return false;
            }

            JumpObstacleData targetObstacle = shiftedObstacles[targetObstacleIndex];
            if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(targetObstacle.Type))
                return false;

            bool isBottomLine = targetObstacle.IsBottomLine;
            for (int obstacleIndex = targetObstacleIndex; obstacleIndex <= resolvedTargetIndex; obstacleIndex++)
            {
                JumpObstacleData obstacle = shiftedObstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.Type))
                    continue;

                if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(obstacle.Type))
                    return false;
            }

            return true;
        }

        #endregion

    }
}
