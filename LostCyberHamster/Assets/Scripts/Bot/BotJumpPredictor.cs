using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Предсказывает исход прыжка, переиспользуя логику из <see cref="CollisionUtils"/>.
    /// Бот вызывает <see cref="PredictJump"/> перед принятием решения "прыгать" —
    /// если результат показывает damage, бот ждёт пока хомяк не подъедет ближе.
    /// </summary>
    public class BotJumpPredictor
    {
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // совпадает с JumpMechanics

        private float _jumpClipWorldShift;
        private float _jumpClipHalfY;
        private float _hamsterWidth;
        private float _hamsterHeight;
        private bool _initialized;

        /// <summary>
        /// Инициализирует предиктор данными из хомяка (вызывается один раз при старте).
        /// </summary>
        public void Initialize(Hamster hamster)
        {
            var ctrl = hamster.GetComponentInChildren<TransformAnimatorController>();
            if (ctrl == null)
            {
                DebugManager.DiagLog("[BotJumpPredictor] TransformAnimatorController not found!");
                return;
            }

            _jumpClipWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, "transform_jump");
            _jumpClipHalfY = HelpMethods.GetClipRootYAtHalf(ctrl, "transform_jump");
            _hamsterWidth = hamster.ColliderWidth;
            _hamsterHeight = hamster.ColliderHeight;
            _initialized = true;

            DebugManager.DiagLog($"[BotJumpPredictor] Initialized: worldShift={_jumpClipWorldShift:F2}, " +
                                 $"halfY={_jumpClipHalfY:F2}, hamsterW={_hamsterWidth:F3}, hamsterH={_hamsterHeight:F3}");
        }

        /// <summary>
        /// Дистанция горизонтального смещения хомяка за один прыжок (мировые координаты).
        /// Используется для проверки consecutive obstacles.
        /// </summary>
        public float JumpShiftDistance => _jumpClipWorldShift;

        /// <summary>
        /// Высота центра хомяка в середине прыжка (относительно земли).
        /// Используется для проверки bigAlive Y-перекрытия в симуляции.
        /// </summary>
        public float JumpMidY => _jumpClipHalfY;

        /// <summary>
        /// Предсказывает исход прыжка для конкретного препятствия, если бот прыгнет прямо сейчас.
        /// Использует ту же логику, что и <see cref="JumpMechanics"/>.
        /// </summary>
        /// <param name="hamsterTransform">Transform хомяка.</param>
        /// <param name="threat">Препятствие из сканера.</param>
        /// <param name="sameLaneObstacles">Все препятствия на текущей линии (для проверки RoadAndRoof на крыше).</param>
        /// <returns>Предсказанный стейт после прыжка.</returns>
        public JumpPrediction PredictJump(
            Transform hamsterTransform,
            ThreatInfo threat,
            IReadOnlyList<ThreatInfo> sameLaneObstacles)
        {
            if (!_initialized)
                return JumpPrediction.Unknown;

            var obstacle = threat.Obstacle;
            if (obstacle == null)
                return JumpPrediction.Unknown;

            switch (threat.Type)
            {
                case ObstacleTypeEnum.smallAlive:
                    return PredictSmallAlive(hamsterTransform, obstacle);

                case ObstacleTypeEnum.smallNotAliveRoad:
                    return PredictSmallNotAliveRoad(hamsterTransform, obstacle);

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    return PredictSmallNotAliveRoadAndRoof(hamsterTransform, obstacle, sameLaneObstacles);

                case ObstacleTypeEnum.bigAlive:
                    return PredictBigAlive(hamsterTransform, obstacle);

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return PredictBigNotAlive(hamsterTransform, obstacle, sameLaneObstacles);

                default:
                    return JumpPrediction.Unknown;
            }
        }

        /// <summary>
        /// Безопасно ли прыгать прямо сейчас? True, если прыжок не приведёт к урону.
        /// </summary>
        public bool IsSafeToJump(
            Transform hamsterTransform,
            ThreatInfo threat,
            IReadOnlyList<ThreatInfo> sameLaneObstacles)
        {
            var prediction = PredictJump(hamsterTransform, threat, sameLaneObstacles);
            return prediction != JumpPrediction.Damage && prediction != JumpPrediction.Unknown;
        }

        // ───── Обработчики по типу (зеркалят JumpMechanics) ─────

        private JumpPrediction PredictSmallAlive(Transform hamster, Obstacle obs)
        {
            float rightTol = _hamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                    hamster, _jumpClipWorldShift, obs, rightTol))
                return JumpPrediction.JumpOnObstacle;

            if (CollisionUtils.IsOverlapAtShift(hamster, _hamsterWidth, _jumpClipWorldShift, obs))
                return JumpPrediction.Damage;

            if (CollisionUtils.IsJumpOver(hamster, _hamsterWidth, _jumpClipWorldShift, obs))
                return JumpPrediction.JumpOver;

            return JumpPrediction.NoHit;
        }

        private JumpPrediction PredictSmallNotAliveRoad(Transform hamster, Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(hamster, _hamsterWidth, _jumpClipWorldShift, obs))
                return JumpPrediction.Damage;

            if (CollisionUtils.IsJumpOver(hamster, _hamsterWidth, _jumpClipWorldShift, obs))
                return JumpPrediction.JumpOver;

            return JumpPrediction.NoHit;
        }

        private JumpPrediction PredictSmallNotAliveRoadAndRoof(
            Transform hamster, Obstacle small, IReadOnlyList<ThreatInfo> sameLaneObstacles)
        {
            if (CollisionUtils.IsJumpOver(hamster, _hamsterWidth, _jumpClipWorldShift, small))
                return JumpPrediction.JumpOver;

            if (!CollisionUtils.IsOverlapAtShift(hamster, _hamsterWidth, _jumpClipWorldShift, small))
                return JumpPrediction.NoHit;

            // Проверяем: лежит ли small на крыше bigNotAlive
            var obstacles = ExtractObstacles(sameLaneObstacles);
            if (CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive(small, obstacles, out _))
            {
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(
                    hamster, _hamsterWidth, _jumpClipWorldShift, obstacles);
                return hitSmall ? JumpPrediction.Damage : JumpPrediction.JumpOnRoof;
            }

            // На дороге — перекрытие = урон
            return JumpPrediction.Damage;
        }

        private JumpPrediction PredictBigAlive(Transform hamster, Obstacle obs)
        {
            // bigAlive: столкновение по X в конце или по Y в середине
            bool hitX = CollisionUtils.IsOverlapAtShift(
                hamster, _hamsterWidth, _jumpClipWorldShift, obs);

            CollisionUtils.GetObstacleYInterval(obs, out var oB, out var oT);
            CollisionUtils.GetHamsterYIntervalAtJumpMid(
                hamster, _hamsterHeight, _jumpClipHalfY, out var hB, out var hT);
            bool hitY = CollisionUtils.IsOverlap(hB, hT, oB, oT);

            if (hitX || hitY)
                return JumpPrediction.Damage;

            return JumpPrediction.NoHit;
        }

        private JumpPrediction PredictBigNotAlive(
            Transform hamster, Obstacle obs, IReadOnlyList<ThreatInfo> sameLaneObstacles)
        {
            if (CollisionUtils.IsOverlapAtShift(hamster, _hamsterWidth, _jumpClipWorldShift, obs))
            {
                var obstacles = ExtractObstacles(sameLaneObstacles);
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(
                    hamster, _hamsterWidth, _jumpClipWorldShift, obstacles);
                return hitSmall ? JumpPrediction.Damage : JumpPrediction.JumpOnRoof;
            }

            return JumpPrediction.NoHit;
        }

        // ───── Helpers ─────

        /// <summary>
        /// Извлекает Obstacle из списка ThreatInfo (для передачи в CollisionUtils).
        /// </summary>
        private static List<Obstacle> ExtractObstacles(IReadOnlyList<ThreatInfo> threats)
        {
            var list = new List<Obstacle>(threats.Count);
            for (int i = 0; i < threats.Count; i++)
            {
                if (threats[i].Obstacle != null)
                    list.Add(threats[i].Obstacle);
            }
            return list;
        }
    }

    /// <summary>
    /// Результат предсказания прыжка.
    /// </summary>
    public enum JumpPrediction
    {
        /// <summary>Не удалось определить.</summary>
        Unknown,

        /// <summary>Хомяк не задевает препятствие (слишком далеко).</summary>
        NoHit,

        /// <summary>Хомяк перелетает препятствие.</summary>
        JumpOver,

        /// <summary>Хомяк успешно напрыгивает на smallAlive.</summary>
        JumpOnObstacle,

        /// <summary>Хомяк запрыгивает на крышу bigNotAlive/mediumNotAlive.</summary>
        JumpOnRoof,

        /// <summary>Хомяк получит урон (overlap, не перелетит).</summary>
        Damage
    }
}
