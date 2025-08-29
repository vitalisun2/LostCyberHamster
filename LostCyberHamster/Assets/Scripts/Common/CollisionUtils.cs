using Assets.Scripts.Gameplay;
using Assets.Scripts.Common.Models;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Common
{
    /// <summary>
    /// Чистые геометрические утилиты: расчёт интервалов и проверка перекрытия.
    /// Версия после правок: препятствия сдвигаем на <paramref name="worldShift"/>,
    /// а границы хомяка берём из свойств Hamster.LeftX / RightX (он по X не движется).
    /// </summary>
    public static class CollisionUtils
    {
        // ───────────────────────────────── X-интервалы ─────────────────────────────────

        /// <summary>X-интервал [left; right] препятствия в конце клипа (учтён worldShift).</summary>
        public static void GetObstacleXInterval(
            Obstacle o,
            float width,
            float worldShift,
            out float left,
            out float right)
        {
            var b = o.GetComponentInChildren<BoxCollider2D>().bounds; // world @ t0
            left = b.min.x - worldShift;     // влево вместе с миром
            right = b.max.x - worldShift;
        }

        /// <summary>Y-интервал [bottom; top] препятствия (без сдвига по Y).</summary>
        public static void GetObstacleYInterval(
            Obstacle o,
            out float bottom,
            out float top)
        {
            var box = o.GetComponentInChildren<BoxCollider2D>();
            if (box == null) { bottom = top = o.transform.position.y; return; }

            Bounds b = box.bounds;            // world
            bottom = b.min.y;
            top = b.max.y;
        }

        /// <summary>
        /// Y-интервал хомяка в середине клипа (при вертикальном смещении <paramref name="midYShift"/>).
        /// </summary>
        public static void GetHamsterYIntervalAtJumpMid(
            Transform hamster,
            float hamsterHeight,
            float midYShift,
            out float bottom,
            out float top)
        {
            float centerY = hamster.position.y + midYShift;
            float half = hamsterHeight * 0.5f;
            bottom = centerY - half;
            top = centerY + half;
        }

        /// <summary>Правая / левая грани хомяка — из кэшированных свойств Hamster.</summary>
        public static void GetHamsterXBounds(
            Transform hamster,
            out float left,
            out float right)
        {
            var h = hamster.GetComponent<Hamster>();
            left = h.LeftX;
            right = h.RightX;
        }

        // ───────────────────────────────── Утилиты перекрытия ─────────────────────────────────

        public static bool IsOverlap(
            float leftA, float rightA,
            float leftB, float rightB,
            float minOverlap) =>
            (rightA - leftB) > minOverlap &&
            (rightB - leftA) > minOverlap;

        public static bool IsOverlap(
            float leftA, float rightA,
            float leftB, float rightB) =>
            (rightA > leftB) && (rightB > leftA);

        // ───────────────────────────────── Проверки в прыжках ─────────────────────────────────

        /// <summary>Пересечение интервалов хомяка (он статичен) и obstacle к концу клипа.</summary>
        public static bool IsOverlapAtShift(
            Transform hamster,
            float _/*hamsterWidth не нужен, оставлен для совместимости*/,
            float worldShift,
            Obstacle obstacle)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, worldShift,
                out var oL, out var oR);

            GetHamsterXBounds(hamster, out var hL, out var hR);

            bool overlap = IsOverlap(hL, hR, oL, oR);
            Debug.Log($"[CollisionUtils.IsOverlapAtShift] hamster=({hL:F3},{hR:F3}) " +
                      $"obs={obstacle.name} ({oL:F3},{oR:F3}) shift={worldShift:F3} overlap={overlap}");
            return overlap;
        }

        /// <summary>True, если хомяк перелетает obstacle полностью по X.</summary>
        public static bool IsJumpOver(
            Transform hamster,
            float hamsterWidth,                 // сохранён для совместимости
            float shift,                        // worldShift клипа
            Obstacle obs)
        {
            // obstacle к концу клипа
            GetObstacleXInterval(obs, obs.ColliderWidth, shift, out var oL, out var oR);

            // хомяк остаётся на месте
            GetHamsterXBounds(hamster, out var hStartL, out var hStartR);
            float hEndL = hStartL;              // не меняется
            float hEndR = hStartR;

            return IsJumpOverIntervals(hStartL, hStartR, hEndL, hEndR, oL, oR, 0f);
        }

        /// <summary>Хит-тест smallNotAliveRoadAndRoof на крыше.</summary>
        public static bool IsHitSmallNotAliveOnRoof(
            Transform hamster,
            float _/*hamsterWidth*/,
            float worldShift,
            IEnumerable<Obstacle> sameLineObstacles)
        {
            GetHamsterXBounds(hamster, out var hL, out var hR);

            foreach (var o in sameLineObstacles)
            {
                if (o.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                GetObstacleXInterval(o, o.ColliderWidth, worldShift, out var oL, out var oR);
                bool overlap = IsOverlap(hL, hR, oL, oR);
                Debug.Log($"[CollisionUtils.IsHitSmallNotAliveOnRoof] hamster=({hL:F3},{hR:F3}) " +
                          $"small={o.name} ({oL:F3},{oR:F3}) shift={worldShift:F3} overlap={overlap}");
                if (overlap) return true;
            }
            return false;
        }

        // ───────────────────────────────── Поиск bigNotAlive под smallNotAlive ─────────────────────────────────

        public static bool TryFindBigNotAliveUnderSmallNotAlive(
            Obstacle smallNotAlive,
            IEnumerable<Obstacle> allObstacles,
            out Obstacle found)
        {
            // текущий кадр — shift = 0
            GetObstacleXInterval(smallNotAlive, smallNotAlive.ColliderWidth, 0f,
                out var smallL, out var smallR);

            foreach (var o in allObstacles)
                if (o.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.bigNotAlive)
                {
                    GetObstacleXInterval(o, o.ColliderWidth, 0f, out var oL, out var oR);
                    bool overlap = IsOverlap(smallL, smallR, oL, oR);
                    if (overlap) { found = o; return true; }
                }

            found = null;
            return false;
        }

        // ───────────────────────────────── Список актуальных препятствий ─────────────────────────────────

        public static IReadOnlyList<Obstacle> GetValidObstaclesAhead(
            Transform hamster,
            bool isOnBottomLine)
        {
            float hx = hamster.position.x;

            return ObstacleSpawner.Instance.SpawnedObstacles
                .Select(io => io.ObstacleScript)
                .Where(o => HelpMethods.IsOnSameLine(isOnBottomLine, o))
                .Where(o => o.transform.position.x > hx)
                .OrderBy(o => o.transform.position.x)
                .ToList();
        }

        // ───────────────────────────────── Ранний выход по reach ─────────────────────────────────

        public static bool ShouldBreakByReachRight(
            Transform hamster,
            float hamsterWidth,           // сохранён для совместимости
            float reachShift,
            Obstacle obstacle,
            float eps = 1e-4f)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, reachShift,
                out var oL, out _);

            GetHamsterXBounds(hamster, out _, out var hamsterRight);

            bool stop = oL > hamsterRight + eps;
            return stop;
        }

        // ───────────────────────────────── Служебные методы ─────────────────────────────────

        /// <summary>Вспомогательный расчёт «чистого» перелёта через obstacle.</summary>
        private static bool IsJumpOverIntervals(
            float hStartL, float hStartR,
            float hEndL, float hEndR,
            float obsL, float obsR,
            float minOverlap)
        {
            bool clearStart = hStartR < obsL;
            bool clearEnd = hEndL > obsR;
            bool noOverlap = !IsOverlap(hEndL, hEndR, obsL, obsR, minOverlap);
            return clearStart && clearEnd && noOverlap;
        }
    }
}
