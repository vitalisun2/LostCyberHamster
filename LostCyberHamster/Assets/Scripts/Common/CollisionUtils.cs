using Assets.Scripts.Gameplay;
using Assets.Scripts.Common.Models;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Common
{
    /// <summary>
    /// Чистые геометрические утилиты: расчёт интервалов и проверка перекрытия.
    /// </summary>
    public static class CollisionUtils
    {
        /// <summary>X-интервал [left; right] препятствия с учётом edgeTol.</summary>
        public static void GetObstacleXInterval(Obstacle obstacle,
            float _ /*width: больше не нужен*/,
            out float left,
            out float right)
        {
            var box = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (box == null) { left = right = obstacle.transform.position.x; return; }

            Bounds b = box.bounds;          // world-space, учёл offset + scale
            left = b.min.x;
            right = b.max.x;
        }

        /// <summary>Y-интервал [bottom; top] препятствия с учётом edgeTol.</summary>
        public static void GetObstacleYInterval(Obstacle o,
            out float bottom,
            out float top)
        {
            var box = o.GetComponentInChildren<BoxCollider2D>();
            if (box == null) { bottom = top = o.transform.position.y; return; }

            Bounds b = box.bounds;          // world-space, учёл offset + scale
            bottom = b.min.y;
            top = b.max.y;
        }


        /// <summary>
        /// X-интервал хомяка к концу прыжка (учтён worldShift клипа).
        /// </summary>
        public static void GetHamsterXIntervalAtJumpEnd(Transform hamster,
                                                        float hamsterWidth,
                                                        float worldShift,
                                                        out float left,
                                                        out float right)
        {
            float endX = hamster.position.x + worldShift;
            float half = hamsterWidth * 0.5f;
            left = endX - half;
            right = endX + half;
        }

        /// <summary>
        /// Y-интервал хомяка к концу прыжка (вертикальный worldShift отсутствует).
        /// </summary>
        public static void GetHamsterYIntervalAtJumpEnd(Transform hamster,
                                                        float hamsterHeight,
                                                        out float bottom,
                                                        out float top)
        {
            float centerY = hamster.position.y;
            float half = hamsterHeight * 0.5f;
            bottom = centerY - half;
            top = centerY + half;
        }

        /// <summary>Пересечение не менее чем на minOverlap.</summary>
        public static bool IsOverlap(float leftA, float rightA,
                                     float leftB, float rightB,
                                     float minOverlap) =>
            (rightA - leftB) > minOverlap &&
            (rightB - leftA) > minOverlap;

        /// <summary>Быстрая проверка пересечения без минимального порога.</summary>
        public static bool IsOverlap(float leftA, float rightA,
                                     float leftB, float rightB) =>
            (rightA > leftB) && (rightB > leftA);

        /// <summary>
        /// True, если хомяк перелетает препятствие полностью по X.
        /// </summary>
        public static bool IsJumpOverIntervals(
            float hStartL, float hStartR,
            float hEndL, float hEndR,
            float obsL, float obsR,
            float minOverlap)
        {
            bool clearStart = hStartR < obsL;   // хомяк слева
            bool clearEnd = hEndL > obsR;   // хомяк справа
            bool noOverlap = !IsOverlap(hEndL, hEndR, obsL, obsR, minOverlap);

            return clearStart && clearEnd && noOverlap;
        }

        /// <summary>
        /// Пересечение X-интервалов хомяка в конце клипа (учитывает worldShift)
        /// c указанным obstacle.
        /// </summary>
        public static bool IsOverlapAtShift(Transform hamster,
                                            float hamsterWidth,
                                            float worldShift,
                                            Obstacle obstacle)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, out var oL, out var oR);
            GetHamsterXIntervalAtJumpEnd(hamster, hamsterWidth, worldShift,
                                         out var hL, out var hR);
            return IsOverlap(hL, hR, oL, oR);
        }

        /// <summary>
        /// Проверяет, пересекается ли хомяк в конце прыжка (учтён worldShift клипа)
        /// с ЛЮБЫМ smallNotAliveRoadAndRoof из переданного списка препятствий.
        /// Вызывать только в ветках, где хомяк уже на крыше.
        /// </summary>
        public static bool IsHitSmallNotAliveOnRoof(
            Transform hamster,
            float hamsterWidth,
            float worldShift,
            IEnumerable<Obstacle> sameLineObstacles)
        {
            GetHamsterXIntervalAtJumpEnd(hamster, hamsterWidth, worldShift, out var hL, out var hR);

            foreach (var o in sameLineObstacles)
            {
                if (o.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                GetObstacleXInterval(o, o.ColliderWidth, out var oL, out var oR);
                bool overlap = IsOverlap(hL, hR, oL, oR);
                Debug.Log($"[CollisionUtils] IsHitSmallNotAliveOnRoof: hEnd=[{hL:F3};{hR:F3}] small='{o.name}' o=[{oL:F3};{oR:F3}] overlap={overlap}");
                if (overlap) return true;
            }

            return false;
        }

        /// <summary>
        /// Сценарий 1. Проверяем, «сидит» ли на bigNotAlive объект
        /// smallNotAliveRoadAndRoof. Нужен лишь факт пересечения.
        /// </summary>
        public static bool HasSmallNotAliveOnRoof(
            Obstacle              bigNotAlive,
            IEnumerable<Obstacle> allObstacles)
        {
            // X-интервал big
            GetObstacleXInterval(bigNotAlive, bigNotAlive.ColliderWidth,
                                 out var bigL, out var bigR);

            foreach (var o in allObstacles)
                if (o.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                {
                    GetObstacleXInterval(o, o.ColliderWidth, out var oL, out var oR);
                    if (IsOverlap(bigL, bigR, oL, oR))
                        return true;
                }

            return false;
        }

        /// <summary>
        /// Сценарий 2. Уже нашли smallNotAliveRoadAndRoof. Проверяем, лежит ли он
        /// поверх bigNotAlive. При успехе возвращаем bigNotAlive через out,
        /// чтобы присвоить LastObstacle.
        /// </summary>
        public static bool TryFindBigNotAliveUnderSmallNotAlive(
            Obstacle              smallNotAlive,
            IEnumerable<Obstacle> allObstacles,
            out Obstacle          found)
        {
            // X-интервал small
            GetObstacleXInterval(smallNotAlive, smallNotAlive.ColliderWidth,
                                 out var smallL, out var smallR);

            foreach (var o in allObstacles)
                if (o.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.bigNotAlive)
                {
                    GetObstacleXInterval(o, o.ColliderWidth, out var oL, out var oR);
                    if (IsOverlap(smallL, smallR, oL, oR))
                    {
                        found = o;
                        return true;
                    }
                }

            found = null;
            return false;
        }
    }
}
