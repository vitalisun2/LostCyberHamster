using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Проверки немедленной безопасности для SwitchLane.
    /// Unsafe означает столкновение во время смещения между линиями или в момент завершения смещения.
    /// Угроза, которая после смены линии остаётся просто впереди, но не пересекает окно смещения,
    /// не блокирует действие и должна обрабатываться следующим пересчётом.
    /// </summary>
    internal static class SwitchLaneSafety
    {
        private const float LaneSwitchDuration = 0.3f;
        private const float LaneSwitchTravel = LaneSwitchDuration * Assets.Scripts.Consts.GameSpeedBase;
        private const float LaneSwitchSafetyPadding = 0.15f;

        public static bool IsImmediatelySafe(BotSceneSnapshot snapshot, int excludeStableId = 0)
        {
            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            bool sourceIsBottom = snapshot.HamsterOnBottom;
            bool targetIsBottom = !snapshot.HamsterOnBottom;

            return IsImmediatelySafe(
                hamsterLeftX,
                snapshot.HamsterRightX,
                sourceIsBottom,
                targetIsBottom,
                snapshot.VisibleObjects,
                excludeStableId);
        }

        public static bool IsImmediatelySafe(Hamster hamster, int excludeStableId = 0)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return true;

            float hamsterLeftX = hamster.LeftX;
            float hamsterRightX = hamster.RightX;
            bool sourceIsBottom = hamster.IsOnBottomLine.Value;
            bool targetIsBottom = !hamster.IsOnBottomLine.Value;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null)
                    continue;

                var obstacle = inst.ObstacleScript;
                if (excludeStableId != 0 && obstacle.GetInstanceID() == excludeStableId)
                    continue;

                if (!IsThreatType(obstacle.ObstacleType.ObstacleTypeEnum))
                    continue;

                bool obstacleOnBottom = !obstacle.ObstacleType.IsTop;
                if (obstacleOnBottom != sourceIsBottom && obstacleOnBottom != targetIsBottom)
                    continue;

                float leftX = obstacle.transform.position.x - obstacle.ColliderWidth * 0.5f;
                float rightX = obstacle.transform.position.x + obstacle.ColliderWidth * 0.5f;
                if (rightX < hamsterLeftX)
                    continue;

                if (obstacleOnBottom == sourceIsBottom &&
                    WouldHitDuringSourceLanePhase(hamsterLeftX, hamsterRightX, leftX, rightX))
                    return false;

                if (obstacleOnBottom == targetIsBottom &&
                    WouldHitDuringTargetLanePhase(hamsterLeftX, hamsterRightX, leftX, rightX))
                    return false;
            }

            return true;
        }

        private static bool IsThreatType(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallAlive:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsImmediatelySafe(
            float hamsterLeftX,
            float hamsterRightX,
            bool sourceIsBottom,
            bool targetIsBottom,
            IReadOnlyList<ObstacleInfo> obstacles,
            int excludeStableId)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                if (!IsHazardForSwitchLane(obstacle))
                    continue;

                if (excludeStableId != 0 && obstacle.StableId == excludeStableId)
                    continue;

                bool obstacleOnBottom = !obstacle.IsTopLane;
                if (obstacleOnBottom != sourceIsBottom && obstacleOnBottom != targetIsBottom)
                    continue;

                if (obstacle.RightX < hamsterLeftX)
                    continue;

                if (obstacleOnBottom == sourceIsBottom &&
                    WouldHitDuringSourceLanePhase(hamsterLeftX, hamsterRightX, obstacle.LeftX, obstacle.RightX))
                    return false;

                if (obstacleOnBottom == targetIsBottom &&
                    WouldHitDuringTargetLanePhase(hamsterLeftX, hamsterRightX, obstacle.LeftX, obstacle.RightX))
                    return false;
            }

            return true;
        }

        internal static bool IsHazardForSwitchLane(ObstacleInfo obstacle)
        {
            if (obstacle.Category == ObjectCategory.Threat)
                return true;

            // smallAlive может быть смертельно опасным при боковом смещении без прыжка.
            return obstacle.Type == ObstacleTypeEnum.smallAlive;
        }

        internal static bool WouldHitDuringTargetLanePhase(
            float hamsterLeftX,
            float hamsterRightX,
            float obstacleLeftX,
            float obstacleRightX)
        {
            // Во время lane switch мир сдвигается влево. Если swept-interval препятствия
            // пересекает X-интервал хомяка, столкновение возможно в окне смещения.
            float sweptLeftX = obstacleLeftX - LaneSwitchTravel;
            float sweptRightX = obstacleRightX;
            float paddedHamsterLeftX = hamsterLeftX - LaneSwitchSafetyPadding;
            float paddedHamsterRightX = hamsterRightX + LaneSwitchSafetyPadding;
            return CollisionUtils.IsOverlap(paddedHamsterLeftX, paddedHamsterRightX, sweptLeftX, sweptRightX);
        }

        internal static bool WouldHitDuringSourceLanePhase(
            float hamsterLeftX,
            float hamsterRightX,
            float obstacleLeftX,
            float obstacleRightX)
        {
            // На исходной линии до завершения смещения хомяк всё ещё уязвим.
            // Используем тот же swept-interval по окну lane switch.
            float sweptLeftX = obstacleLeftX - LaneSwitchTravel;
            float sweptRightX = obstacleRightX;
            float paddedHamsterLeftX = hamsterLeftX - LaneSwitchSafetyPadding;
            float paddedHamsterRightX = hamsterRightX + LaneSwitchSafetyPadding;
            return CollisionUtils.IsOverlap(paddedHamsterLeftX, paddedHamsterRightX, sweptLeftX, sweptRightX);
        }
    }
}