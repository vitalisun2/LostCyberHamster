using Assets.Scripts.Gameplay;
using Assets.Scripts.Common.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private const int _maxObstaclesPerCheck = 10;

        private struct ObstacleSpan
        {
            public readonly Obstacle Obstacle;
            public readonly float Left;
            public readonly float Right;

            public ObstacleSpan(Obstacle obstacle, float left, float right)
            {
                Obstacle = obstacle;
                Left = left;
                Right = right;
            }
        }

        private static readonly List<Obstacle> _buffer = new(32);      // внутренний пул
        private static readonly ReadOnlyCollection<Obstacle> _roBuffer // read-only обёртка
            = new(_buffer);                                            // аллоцируется один раз
        private static readonly Dictionary<Obstacle, Obstacle> _smallOnBigCache = new();
        private static readonly List<ObstacleSpan> _bigObstacleSpans = new();
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
            float minX = b.min.x;
            float maxX = b.max.x;
            left = minX - worldShift;     // влево вместе с миром
            right = maxX - worldShift;
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

        /// <summary>
        /// Проверяет перекрытие хомяка и препятствия при смещении мира.
        /// </summary>
        /// <returns>True, если есть перекрытие; out – доля перекрытия относительно ширины препятствия.</returns>
        public static bool IsOverlapAtShift(
            Transform hamster,
            float hamsterWidth,
            float worldShift,
            Obstacle obstacle,
            out float overlapFraction)
        {
            GetHamsterXBounds(hamster, out var hL, out var hR);
            return IsOverlapAtShift(hL, hR, worldShift, obstacle, out overlapFraction);
        }

        public static bool IsOverlapAtShift(
            float hamsterLeft,
            float hamsterRight,
            float worldShift,
            Obstacle obstacle,
            out float overlapFraction)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, worldShift,
                                 out var oL, out var oR);

            float minRight = Mathf.Min(hamsterRight, oR);
            float maxLeft = Mathf.Max(hamsterLeft, oL);
            float overlapLen = minRight - maxLeft;
            if (overlapLen < 0f) overlapLen = 0f;
            overlapFraction = overlapLen / obstacle.ColliderWidth;   // 0‒1
            return overlapLen > 0f;
        }

        /// <summary>Пересечение интервалов хомяка (он статичен) и obstacle к концу клипа.</summary>
        public static bool IsOverlapAtShift(
            Transform hamster,
            float _/*hamsterWidth не нужен, оставлен для совместимости*/,
            float worldShift,
            Obstacle obstacle)
        {
            GetHamsterXBounds(hamster, out var hL, out var hR);
            return IsOverlapAtShift(hL, hR, worldShift, obstacle);
        }

        public static bool IsOverlapAtShift(
            float hamsterLeft,
            float hamsterRight,
            float worldShift,
            Obstacle obstacle)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, worldShift,
                out var oL, out var oR);

            return IsOverlap(hamsterLeft, hamsterRight, oL, oR);
        }

        /// <summary>True, если хомяк перелетает obstacle полностью по X.</summary>
        public static bool IsJumpOver(
            Transform hamster,
            float hamsterWidth,
            float shift,
            Obstacle obs,
            float minOverlap = 0f)
        {
            // Границы хомяка (по X не меняются)
            float hx = hamster.position.x;
            float half = hamsterWidth * 0.5f;
            float hL = hx - half;
            float hR = hx + half;

            // Границы препятствия в начале прыжка
            GetObstacleXInterval(obs, obs.ColliderWidth, 0f, out var oStartL, out var oStartR);

            // Границы препятствия в конце прыжка (со сдвигом мира)
            GetObstacleXInterval(obs, obs.ColliderWidth, shift, out var oEndL, out var oEndR);

            return IsJumpOverIntervals(hL, hR, oStartL, oStartR, oEndL, oEndR, minOverlap);
        }

        /// <summary>Хит-тест smallNotAliveRoadAndRoof на крыше.</summary>
        public static bool IsHitSmallNotAliveOnRoof(
            Transform hamster,
            float _/*hamsterWidth*/,
            float worldShift,
            IEnumerable<Obstacle> sameLineObstacles)
        {
            GetHamsterXBounds(hamster, out var hL, out var hR);
            return IsHitSmallNotAliveOnRoof(hL, hR, worldShift, sameLineObstacles);
        }

        public static bool IsHitSmallNotAliveOnRoof(
            float hamsterLeft,
            float hamsterRight,
            float worldShift,
            IEnumerable<Obstacle> sameLineObstacles)
        {
            foreach (var o in sameLineObstacles)
            {
                if (o.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                // пропускаем коробки, которые стоят на дороге,
                //    а не на крыше BigNotAlive
                if (!TryFindBigNotAliveUnderSmallNotAlive(o, sameLineObstacles, out var _)) // ✱
                    continue;

                GetObstacleXInterval(o, o.ColliderWidth, worldShift, out var oL, out var oR);
                bool overlap = IsOverlap(hamsterLeft, hamsterRight, oL, oR);

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
            if (_smallOnBigCache.TryGetValue(smallNotAlive, out found) && found != null)
            {
                return true;
            }

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
            _buffer.Clear();

            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return _roBuffer;

            var source = isOnBottomLine ? spawner.SpawnedObstaclesBottom : spawner.SpawnedObstaclesTop;
            if (source == null || source.Count == 0)
                return _roBuffer;

            int ensureCount = Mathf.Min(source.Count, _maxObstaclesPerCheck);
            if (_buffer.Capacity < ensureCount)
                _buffer.Capacity = ensureCount;

            float hx = hamster.position.x;
            int added = 0;

            for (int i = 0; i < source.Count && added < _maxObstaclesPerCheck; i++)
            {
                var inst = source[i];
                var obstacle = inst.ObstacleScript;

                if (obstacle == null)
                    continue;

                if (obstacle.transform.position.x <= hx)
                    continue;

                _buffer.Add(obstacle);
                added++;
            }

            BuildSmallOnBigCache(_buffer);

            return _roBuffer;      // наружу отдаём только неизменяемый вид
        }

        private static void BuildSmallOnBigCache(List<Obstacle> obstacles)
        {
            _smallOnBigCache.Clear();
            _bigObstacleSpans.Clear();

            if (obstacles.Count == 0)
                return;

            foreach (var obstacle in obstacles)
            {
                if (obstacle == null)
                    continue;

                if (obstacle.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.bigNotAlive)
                {
                    GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out var left, out var right);
                    _bigObstacleSpans.Add(new ObstacleSpan(obstacle, left, right));
                }
            }

            if (_bigObstacleSpans.Count == 0)
                return;

            int bigIndex = 0;

            foreach (var obstacle in obstacles)
            {
                if (obstacle == null)
                    continue;

                if (obstacle.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out var smallLeft, out var smallRight);

                while (bigIndex < _bigObstacleSpans.Count && _bigObstacleSpans[bigIndex].Right < smallLeft)
                    bigIndex++;

                for (int j = bigIndex; j < _bigObstacleSpans.Count; j++)
                {
                    var span = _bigObstacleSpans[j];
                    if (span.Left > smallRight)
                        break;

                    if (IsOverlap(smallLeft, smallRight, span.Left, span.Right))
                    {
                        _smallOnBigCache[obstacle] = span.Obstacle;
                        break;
                    }
                }
            }
        }

        // ───────────────────────────────── Ранний выход по reach ─────────────────────────────────

        public static bool ShouldBreakByReachRight(
            Transform hamster,
            float hamsterWidth,           // сохранён для совместимости
            float reachShift,
            Obstacle obstacle,
            float eps = 1e-4f)
        {
            GetHamsterXBounds(hamster, out _, out var hamsterRight);
            return ShouldBreakByReachRight(hamsterRight, reachShift, obstacle, eps);
        }

        public static bool ShouldBreakByReachRight(
            float hamsterRight,
            float reachShift,
            Obstacle obstacle,
            float eps = 1e-4f)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, reachShift,
                out var oL, out _);

            bool stop = oL > hamsterRight + eps;
            return stop;
        }

        // ───────────────────────────────── Служебные методы ─────────────────────────────────

        /// <summary>Вспомогательный расчёт «чистого» перелёта через obstacle.</summary>
        private static bool IsJumpOverIntervals(
            float hL, float hR,
            float oStartL, float oStartR,
            float oEndL, float oEndR,
            float minOverlap)
        {
            bool clearStart = hR < oStartL;
            bool clearEnd = hL > oEndR;
            bool noOverlap = !IsOverlap(hL, hR, oEndL, oEndR, minOverlap);
            return clearStart && clearEnd && noOverlap;
        }

        /// <summary>
        /// Определяет, находится ли центр хомяка (hamster.position.x) внутри X-интервала
        /// препятствия после смещения на <paramref name="worldShift"/>.<br/>
        /// • Для <b>SmallAlive</b> (по умолчанию) используем фактический [left; right].<br/>
        /// • Для <b>bigAlive</b> интервал расширяем на 30 % ширины влево и вправо: высокие,
        ///   но узкие объекты хомяк визуально “зацепляет головой” ещё до завершения прыжка.<br/>
        /// Возвращает <c>true</c>, если центр внутри скорректированного интервала.
        /// Дополнительный допуск можно задать параметром <paramref name="rightTol"/>,
        /// который расширяет правую границу препятствия.
        /// </summary>

        /// <param name="rightTol">
        /// Допуск по правому краю: насколько можно выйти за <c>right</c>,
        /// чтобы всё ещё считать центр внутри препятствия.
        /// </param>
        public static bool IsHamsterCenterInsideObstacleAtShift(
            Transform hamster,
            float worldShift,
            Obstacle obstacle,
            float rightTol = 0f)
        {
            GetObstacleXInterval(obstacle, obstacle.ColliderWidth, worldShift,
                                 out var left, out var right);

            if (obstacle.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.bigAlive)
            {
                float thirdFraction = obstacle.ColliderWidth * 0.3f;
                left -= thirdFraction;
                right += thirdFraction;
            }

            right += rightTol; // допуск только по правому краю

            float centerX = hamster.position.x;
            return centerX >= left && centerX <= right;
        }
    }
}
