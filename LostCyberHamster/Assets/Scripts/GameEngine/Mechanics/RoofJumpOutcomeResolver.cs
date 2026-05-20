using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Определяет итог обычного прыжка с крыши по тем же правилам, что и runtime roof-jump механика.
    /// Resolver не работает с Unity-объектами напрямую: вызывающий код передает снимок препятствий
    /// и геометрию хомяка через <see cref="JumpObstacleData"/> и <see cref="RoofJumpResolveContext"/>.
    /// </summary>
    public static class RoofJumpOutcomeResolver
    {
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // 20 % ширины хомяка
        private const int NoTarget = -1;

        /// <summary>
        /// Обходит препятствия впереди хомяка и через обработчик по типу препятствия
        /// определяет итог roof jump: прыжок по крыше, сход с крыши, урон или напрыг.
        /// </summary>
        public static JumpResolveResult ResolveRoofJump(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            JumpResolveResult noHit = new(HamsterStateEnum.JumpFromRoof, NoTarget);

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != context.IsBottomLine)
                    continue;

                if (obstacle.CenterX <= context.HamsterCenterX)
                    continue;

                // Корректный ранний выход по левой грани препятствия.
                if (ShouldBreakByReachRight(context, obstacle))
                {
                    LogBreak(obstacleIndex, obstacle, context);
                    break;
                }

                LogCandidate(obstacleIndex, obstacle, context);

                JumpResolveResult result = HandleObstacle(
                    obstacle,
                    obstacleIndex,
                    obstacles,
                    context,
                    noHit);
                LogResult(obstacleIndex, obstacle, result, noHit, context);

                if (result.State != noHit.State)
                    return result;
            }

            return noHit;
        }

        private static JumpResolveResult HandleObstacle(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            switch (obstacle.Type)
            {
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return HandleBigNotAlive(obstacle, obstacleIndex, obstacles, context, noHit);
                case ObstacleTypeEnum.bigAlive:
                    return HandleBigAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallAlive:
                    return HandleSmallAlive(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoad:
                    return HandleSmallNotAliveRoad(obstacle, obstacleIndex, context, noHit);
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return HandleSmallNotAliveRoadAndRoof(obstacle, obstacleIndex, obstacles, context, noHit);
                default:
                    return noHit;
            }
        }

        // ----- Обработчики --------------------------------------------------------

        // bigNotAlive -> RoofJump
        private static JumpResolveResult HandleBigNotAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (!IsOverlapAtShift(context, obstacle, context.RoofJumpShift))
                return noHit;

            bool hitSmall = IsHitSmallNotAliveOnRoof(obstacles, context);
            HamsterStateEnum state = hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump;

            return new JumpResolveResult(state, obstacleIndex);
        }

        // bigAlive -> JumpOnObstacleFromRoof / JumpFromRoofDamage
        private static JumpResolveResult HandleBigAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            // 1. Центр внутри границ препятствия? -> удачный напрыг
            float rightTolerance = context.HamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (IsHamsterCenterInsideObstacleAtShift(
                    context,
                    obstacle,
                    context.JumpFromRoofShift,
                    rightTolerance))
            {
                return new JumpResolveResult(HamsterStateEnum.JumpOnObstacleFromRoof, obstacleIndex);
            }

            // 2. Иначе: есть ли вообще X-пересечение? -> урон
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex);

            // 3. Вообще не задели
            return noHit;
        }

        // smallAlive -> JumpOnObstacleFromRoof / JumpFromRoofDamage
        private static JumpResolveResult HandleSmallAlive(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            // 1. Центр внутри границ препятствия? -> удачный напрыг
            float rightTolerance = context.HamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (IsHamsterCenterInsideObstacleAtShift(
                    context,
                    obstacle,
                    context.JumpFromRoofShift,
                    rightTolerance))
            {
                return new JumpResolveResult(HamsterStateEnum.JumpOnObstacleFromRoof, obstacleIndex);
            }

            // 2. Иначе: есть ли вообще X-пересечение? -> урон
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex);

            // 3. Вообще не задели
            return noHit;
        }

        // smallNotAliveRoad -> JumpFromRoofDamage
        private static JumpResolveResult HandleSmallNotAliveRoad(
            JumpObstacleData obstacle,
            int obstacleIndex,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            if (IsOverlapAtShift(context, obstacle, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, obstacleIndex);

            return noHit;
        }

        // smallNotAliveRoadAndRoof -> RoofJump / RoofJumpDamage / JumpFromRoofDamage
        private static JumpResolveResult HandleSmallNotAliveRoadAndRoof(
            JumpObstacleData small,
            int smallIndex,
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context,
            JumpResolveResult noHit)
        {
            bool isOnBigRoof = TryFindBigNotAliveUnderSmallNotAlive(
                small,
                obstacles,
                context,
                out int bigUnderSmallIndex);

            if (isOnBigRoof)
            {
                bool hitSmall = IsHitSmallNotAliveOnRoof(obstacles, context);
                HamsterStateEnum state = hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump;
                return new JumpResolveResult(state, bigUnderSmallIndex);
            }

            // small стоит на дороге: прыгаем с крыши вниз.
            if (IsOverlapAtShift(context, small, context.JumpFromRoofShift))
                return new JumpResolveResult(HamsterStateEnum.JumpFromRoofDamage, smallIndex);

            return noHit;
        }

        /// <summary>
        /// Хит-тест smallNotAliveRoadAndRoof на крыше.
        /// Повторяет смысл runtime helper'а CollisionUtils.IsHitSmallNotAliveOnRoof.
        /// </summary>
        private static bool IsHitSmallNotAliveOnRoof(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                if (!IsSameRuntimeCandidate(obstacle, context))
                    continue;

                // Пропускаем коробки, которые стоят на дороге, а не на крыше BigNotAlive/MediumNotAlive.
                if (!TryFindBigNotAliveUnderSmallNotAlive(obstacle, obstacles, context, out _))
                    continue;

                if (IsOverlapAtShift(context, obstacle, context.RoofJumpShift))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Ищет roof obstacle под smallNotAliveRoadAndRoof в текущем снимке препятствий.
        /// Повторяет смысл runtime helper'а CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive.
        /// </summary>
        private static bool TryFindBigNotAliveUnderSmallNotAlive(
            JumpObstacleData smallNotAlive,
            IReadOnlyList<JumpObstacleData> allObstacles,
            RoofJumpResolveContext context,
            out int foundIndex)
        {
            for (int obstacleIndex = 0; obstacleIndex < allObstacles.Count; obstacleIndex++)
            {
                JumpObstacleData candidate = allObstacles[obstacleIndex];
                if (!IsSameRuntimeCandidate(candidate, context))
                    continue;

                if (!CollisionUtils.IsRoofObstacle(candidate.Type))
                    continue;

                if (CollisionUtils.IsOverlap(
                        smallNotAlive.LeftX,
                        smallNotAlive.RightX,
                        candidate.LeftX,
                        candidate.RightX))
                {
                    foundIndex = obstacleIndex;
                    return true;
                }
            }

            foundIndex = NoTarget;
            return false;
        }

        private static bool IsSameRuntimeCandidate(
            JumpObstacleData obstacle,
            RoofJumpResolveContext context)
        {
            return obstacle.IsBottomLine == context.IsBottomLine
                   && obstacle.CenterX > context.HamsterCenterX;
        }

        private static bool ShouldBreakByReachRight(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle)
        {
            return obstacle.LeftX - context.ReachShift > context.HamsterRightX + 0.0001f;
        }

        private static bool IsOverlapAtShift(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle,
            float shift)
        {
            return CollisionUtils.IsOverlap(
                context.HamsterLeftX,
                context.HamsterRightX,
                obstacle.LeftX - shift,
                obstacle.RightX - shift);
        }

        private static void LogCandidate(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context)
        {
            if (!context.LogDiagnostics)
                return;

            float roofLeft = obstacle.LeftX - context.RoofJumpShift;
            float roofRight = obstacle.RightX - context.RoofJumpShift;
            float jumpFromRoofLeft = obstacle.LeftX - context.JumpFromRoofShift;
            float jumpFromRoofRight = obstacle.RightX - context.JumpFromRoofShift;
            bool roofOverlap = CollisionUtils.IsOverlap(
                context.HamsterLeftX,
                context.HamsterRightX,
                roofLeft,
                roofRight);
            bool jumpFromRoofOverlap = CollisionUtils.IsOverlap(
                context.HamsterLeftX,
                context.HamsterRightX,
                jumpFromRoofLeft,
                jumpFromRoofRight);

            DebugManager.DiagLog(
                $"[RoofJumpShift CAND] idx={obstacleIndex} type={obstacle.Type} " +
                $"hamster=[{context.HamsterLeftX:F3},{context.HamsterRightX:F3}] " +
                $"start=[{obstacle.LeftX:F3},{obstacle.RightX:F3}] " +
                $"roofShift={context.RoofJumpShift:F3} roofEnd=[{roofLeft:F3},{roofRight:F3}] roofOverlap={roofOverlap} " +
                $"jumpFromRoofShift={context.JumpFromRoofShift:F3} jumpFromRoofEnd=[{jumpFromRoofLeft:F3},{jumpFromRoofRight:F3}] " +
                $"jumpFromRoofOverlap={jumpFromRoofOverlap}");
        }

        private static void LogResult(
            int obstacleIndex,
            JumpObstacleData obstacle,
            JumpResolveResult result,
            JumpResolveResult noHit,
            RoofJumpResolveContext context)
        {
            if (!context.LogDiagnostics)
                return;

            DebugManager.DiagLog(
                $"[RoofJumpShift RESULT] idx={obstacleIndex} type={obstacle.Type} " +
                $"state={result.State} targetIndex={result.TargetIndex} returns={result.State != noHit.State}");
        }

        private static void LogBreak(
            int obstacleIndex,
            JumpObstacleData obstacle,
            RoofJumpResolveContext context)
        {
            if (!context.LogDiagnostics)
                return;

            DebugManager.DiagLog(
                $"[RoofJumpShift BREAK] idx={obstacleIndex} type={obstacle.Type} " +
                $"left={obstacle.LeftX:F3} reachShift={context.ReachShift:F3} " +
                $"hamsterRight={context.HamsterRightX:F3}");
        }

        private static bool IsHamsterCenterInsideObstacleAtShift(
            RoofJumpResolveContext context,
            JumpObstacleData obstacle,
            float shift,
            float rightTolerance)
        {
            float left = obstacle.LeftX - shift;
            float right = obstacle.RightX - shift;

            if (obstacle.Type == ObstacleTypeEnum.bigAlive)
            {
                float thirdFraction = (obstacle.RightX - obstacle.LeftX) * 0.3f;
                left -= thirdFraction;
                right += thirdFraction;
            }

            right += rightTolerance;

            return context.HamsterCenterX >= left && context.HamsterCenterX <= right;
        }
    }
}
