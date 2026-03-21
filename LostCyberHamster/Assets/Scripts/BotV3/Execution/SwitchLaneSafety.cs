using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Проверки безопасности SwitchLane: не столкнётся ли хомяк
    /// во время смещения между линиями.
    ///
    /// Модель коллизий в runtime:
    ///   1. TapRequest → IsOnBottomLine мгновенно переключается на target lane.
    ///   2. CollisionController.ProcessTrigerEnter проверяет IsOnSameLine(IsOnBottomLine, obstacle).
    ///   3. Source-lane препятствия: IsOnSameLine сразу false → коллизии невозможны.
    ///   4. Target-lane препятствия: IsOnSameLine true, но физическое пересечение trigger'ов
    ///      происходит только когда collider хомяка достигнет target Y.
    ///
    /// IsImmediatelySafe используется в StepExecutor перед fire для live-проверки.
    /// На этапе планирования (ActionGenerator) safety check не нужен — StepExecutor
    /// откладывает fire пока live-проверка не пройдёт.
    /// </summary>
    internal static class SwitchLaneSafety
    {
        private const float LaneSwitchDuration = 0.3f;
        private const float LaneSwitchTravel = LaneSwitchDuration * Assets.Scripts.Consts.GameSpeedBase;
        private const float ReturnControlDuration = 0.47f;
        private const float TargetLaneFullTravel =
            (LaneSwitchDuration + ReturnControlDuration) * Assets.Scripts.Consts.GameSpeedBase;
        private const float LaneSwitchSafetyPadding = 0.15f;

        /// <summary>
        /// Проверяет, безопасно ли выполнить SwitchLane прямо сейчас.
        /// Snapshot-версия для BranchGenerator idle-period checks.
        /// </summary>
        public static bool IsImmediatelySafe(BotSceneSnapshot snapshot)
        {
            float hamsterLeftX = snapshot.HamsterRightX - snapshot.HamsterWidth;
            bool targetIsBottom = !snapshot.HamsterOnBottom;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (!IsHazard(obstacle)) continue;

                bool obstacleOnBottom = !obstacle.IsTopLane;
                if (obstacleOnBottom != targetIsBottom)
                    continue;

                if (obstacle.RightX < hamsterLeftX)
                    continue;

                if (WouldHitDuringTargetPhase(hamsterLeftX, snapshot.HamsterRightX, obstacle.LeftX, obstacle.RightX))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Проверяет, безопасно ли выполнить SwitchLane прямо сейчас.
        /// Live-версия для StepExecutor (читает ObstacleSpawner напрямую).
        /// </summary>
        public static bool IsImmediatelySafe(Hamster hamster)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return true;

            float hamsterLeftX = hamster.LeftX;
            float hamsterRightX = hamster.RightX;
            bool targetIsBottom = !hamster.IsOnBottomLine.Value;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                var obs = inst.ObstacleScript;
                if (!IsThreatType(obs.ObstacleType.ObstacleTypeEnum)) continue;

                bool obstacleOnBottom = !obs.ObstacleType.IsTop;
                if (obstacleOnBottom != targetIsBottom)
                    continue;

                float leftX = obs.transform.position.x - obs.ColliderWidth * 0.5f;
                float rightX = obs.transform.position.x + obs.ColliderWidth * 0.5f;
                if (rightX < hamsterLeftX) continue;

                if (WouldHitDuringTargetPhase(hamsterLeftX, hamsterRightX, leftX, rightX))
                    return false;
            }

            return true;
        }

        private static bool IsHazard(ObstacleInfo obstacle)
        {
            if (obstacle.Category == ObjectCategory.Threat)
                return true;
            return obstacle.Type == ObstacleTypeEnum.smallAlive;
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

        private static bool WouldHitDuringTargetPhase(
            float hamsterLeftX, float hamsterRightX,
            float obstacleLeftX, float obstacleRightX)
        {
            float sweptLeftX = obstacleLeftX - TargetLaneFullTravel;
            float sweptRightX = obstacleRightX;
            return CollisionUtils.IsOverlap(
                hamsterLeftX - LaneSwitchSafetyPadding,
                hamsterRightX + LaneSwitchSafetyPadding,
                sweptLeftX, sweptRightX);
        }
    }
}
