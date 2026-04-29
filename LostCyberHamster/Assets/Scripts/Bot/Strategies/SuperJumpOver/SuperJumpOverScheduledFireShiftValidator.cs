using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Проверяет, что сохранённый fire shift super jump-over всё ещё валиден.
    /// </summary>
    internal sealed class SuperJumpOverScheduledFireShiftValidator
    {
        /// <summary>
        /// Проверяет, что action всё ещё может выполнить ожидаемый super jump-over по исходной цели.
        /// </summary>
        public bool IsScheduledFireShiftStillValid(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            PlannedAction action,
            float validationEpsilon)
        {
            if (planningState == null || projectedWorldSnapshot == null || targetObstacle == null || action == null)
                return false;

            if (!TryGetFireShiftSearchWindow(
                    planningState,
                    projectedWorldSnapshot,
                    targetObstacle,
                    targetObstacleIndex,
                    action.PostFireWorldShift,
                    out float firstFireShift,
                    out float lastFireShift))
            {
                return false;
            }

            if (!TryGetRemainingFireShift(projectedWorldSnapshot, targetObstacle, action, out float fireShift))
                return false;

            if (fireShift < firstFireShift - validationEpsilon || fireShift > lastFireShift + validationEpsilon)
                return false;

            List<JumpObstacleData> baseObstacles = BuildBaseObstacles(projectedWorldSnapshot);
            return IsExpectedOutcomeAtFireShift(
                planningState.Hamster,
                baseObstacles,
                fireShift,
                action.PostFireWorldShift,
                targetObstacleIndex);
        }

        /// <summary>
        /// Получает физически допустимое окно запуска super jump-over.
        /// </summary>
        private static bool TryGetFireShiftSearchWindow(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float superJumpTravel,
            out float firstFireShift,
            out float lastFireShift)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            float chainRightX = GetRoadSmallChainRightX(
                projectedWorldSnapshot,
                targetObstacle,
                targetObstacleIndex);

            firstFireShift = chainRightX - hamster.HamsterLeftX - superJumpTravel;
            if (firstFireShift < 0f)
                firstFireShift = 0f;

            lastFireShift = targetObstacle.LeftX - hamster.HamsterRightX;
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

        /// <summary>
        /// Восстанавливает оставшийся fire shift для retained action по live trigger obstacle.
        /// </summary>
        private static bool TryGetRemainingFireShift(
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot targetObstacle,
            PlannedAction action,
            out float fireShift)
        {
            if (projectedWorldSnapshot == null || targetObstacle == null || action == null)
            {
                fireShift = 0f;
                return false;
            }

            int? triggerObstacleInstanceId = action.TriggerObstacleInstanceId ?? action.TargetObstacleInstanceId;
            if (triggerObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = projectedWorldSnapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != triggerObstacleInstanceId.Value)
                        continue;

                    fireShift = obstacle.LeftX - action.TriggerX;
                    return true;
                }
            }

            fireShift = targetObstacle.LeftX - action.TriggerX;
            return true;
        }

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
        /// Строит obstacles в координатах момента fire shift.
        /// </summary>
        private static List<JumpObstacleData> BuildShiftedObstacles(
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift)
        {
            var shiftedObstacles = new List<JumpObstacleData>(baseObstacles.Count);
            for (int obstacleIndex = 0; obstacleIndex < baseObstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = baseObstacles[obstacleIndex];
                shiftedObstacles.Add(new JumpObstacleData(
                    obstacle.Type,
                    obstacle.IsBottomLine,
                    obstacle.LeftX - fireShift,
                    obstacle.RightX - fireShift,
                    obstacle.CenterX - fireShift,
                    obstacle.HasY,
                    obstacle.BottomY,
                    obstacle.TopY));
            }

            return shiftedObstacles;
        }

        /// <summary>
        /// Проверяет, что fire shift приводит ровно к SuperJumpOver по ожидаемому obstacle.
        /// </summary>
        private static bool IsExpectedOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float superJumpTravel,
            int targetObstacleIndex)
        {
            List<JumpObstacleData> obstaclesAtFireShift = BuildShiftedObstacles(baseObstacles, fireShift);
            JumpResolveResult result = GetRuntimeOutcome(
                hamster,
                obstaclesAtFireShift,
                superJumpTravel);

            return result.State == HamsterStateEnum.SuperJumpOver
                   && IsExpectedTarget(obstaclesAtFireShift, targetObstacleIndex, result.TargetIndex);
        }

        /// <summary>
        /// Возвращает результат runtime resolver'а для obstacles в момент fire.
        /// </summary>
        private static JumpResolveResult GetRuntimeOutcome(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
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

            return SuperJumpOutcomeResolver.ResolveSuperJump(shiftedObstacles, context);
        }

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
    }
}
