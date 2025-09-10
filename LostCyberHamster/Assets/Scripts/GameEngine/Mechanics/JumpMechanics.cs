using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using System;
using System.Collections.Generic;
using Assets.Scripts.GameEngine.Mechanics.Models;
using UnityEngine;
using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Отвечает за расчёт логики прыжка хомяка и запуск соответствующих анимаций /
    /// игровых событий.
    /// </summary>
    public class JumpMechanics
    {
        private readonly AtomicVariable<int> _energy;
        private readonly AtomicVariable<bool> _isOnBottomLine;
        private readonly AtomicEvent _jumpRequest;
        private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly AtomicVariable<bool> _isDamaged;
        private readonly TransformAnimatorController _transformAnimatorController;
        private readonly SpriteAnimatorController _spriteAnimatorController;

        private const string CLIP_JUMP = "transform_jump";
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // 20 % ширины хомяка
        private readonly float _jumpClipWorldShift;
        private readonly float _jumpClipHalfY;

        private readonly Transform _characterTransform;
        private readonly AtomicVariable<Obstacle> _lastObstacle;
        private readonly float _hamsterWidthInUnits;
        private readonly float _hamsterHeightInUnits;

        // список препятствий, уже лежащих на нужной линии
        private IReadOnlyList<Obstacle> _sameLineObstacles;

        private readonly Dictionary<ObstacleTypeEnum, Func<Obstacle, JumpResult>> _handlers;
        private readonly JumpResult _noHit = new(HamsterStateEnum.Jump, null);

        public JumpMechanics(
            AtomicVariable<int> energy,
            AtomicVariable<bool> isOnBottomLine,
            AtomicEvent jumpRequest,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isDamaged,
            TransformAnimatorController transformAnimatorController,
            SpriteAnimatorController spriteAnimatorController,
            Transform characterTransform,
            AtomicVariable<Obstacle> lastObstacle,
            float hamsterWidthInUnits,
            float hamsterHeightInUnits)
        {
            _energy = energy;
            _isOnBottomLine = isOnBottomLine;
            _jumpRequest = jumpRequest;
            _hamsterState = hamsterState;
            _isDamaged = isDamaged;
            _transformAnimatorController = transformAnimatorController;
            _spriteAnimatorController = spriteAnimatorController;
            _characterTransform = characterTransform;
            _lastObstacle = lastObstacle;
            _hamsterWidthInUnits = hamsterWidthInUnits;
            _hamsterHeightInUnits = hamsterHeightInUnits;
            _jumpClipWorldShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_JUMP);
            _jumpClipHalfY = HelpMethods.GetClipRootYAtHalf(
                _transformAnimatorController, CLIP_JUMP);

            // будет заполнено при вычислении состояния прыжка
            _sameLineObstacles = Array.Empty<Obstacle>();

            _handlers = new()
            {
                { ObstacleTypeEnum.smallAlive,               HandleSmallAlive          },
                { ObstacleTypeEnum.smallNotAliveRoad,        HandleSmallNotAliveRoad   }, // ← новое имя
                { ObstacleTypeEnum.smallNotAliveRoadAndRoof, HandleSmallNotAliveRoadAndRoof    }, // ← новый обработчик
                { ObstacleTypeEnum.bigAlive,                 HandleBigAlive            },
                { ObstacleTypeEnum.bigNotAlive,              HandleBigNotAlive         },
            };
        }

        public void OnEnable() => _jumpRequest.Subscribe(OnJump);
        public void OnDisable() => _jumpRequest.Unsubscribe(OnJump);

        /// <summary>
        /// Запускает расчёт и анимацию прыжка, если хватает энергии.
        /// </summary>
        private void OnJump()
        {
            if (_energy.Value < 10) return;

            var result = CalculateJumpState();
            _hamsterState.Value = result.State;
            if (result.Target != null) _lastObstacle.Value = result.Target;

            SendJumpEventIfNeeded(result);

            _transformAnimatorController.SetJumpAnimationTrigger(_hamsterState);
            _spriteAnimatorController.Jump();
        }

        private void SendJumpEventIfNeeded(JumpResult result)
        {
            switch (result.State)
            {
                case HamsterStateEnum.JumpOnObstacle:
                    GameEventsManager.ObstacleJumpedOn(result.Target!.name);
                    break;
                case HamsterStateEnum.JumpOver:
                    GameEventsManager.ObstacleJumpedOver(result.Target!.name);
                    break;
            }
        }

        /// <summary>
        /// Определяет итог прыжка, группируя логику по типу препятствия.
        /// </summary>
        private JumpResult CalculateJumpState()
        {
            if (_isDamaged.Value)
                return new JumpResult(HamsterStateEnum.Jump, null);

            var obstacles = CollisionUtils.GetValidObstaclesAhead(_characterTransform, _isOnBottomLine.Value);
            _sameLineObstacles = obstacles;
            float reachShift = _jumpClipWorldShift;

            foreach (var obs in obstacles)
            {
                if (CollisionUtils.ShouldBreakByReachRight(_characterTransform, _hamsterWidthInUnits, reachShift, obs))
                    break;

                if (_handlers.TryGetValue(obs.ObstacleType.ObstacleTypeEnum, out var handler))
                {
                    var res = handler(obs);
                    if (res.State != HamsterStateEnum.Jump)
                        return res;
                }
            }

            return new JumpResult(HamsterStateEnum.Jump, null);
        }


        // ───── Обработчики ──────────────────────────────────────────────────────

        private JumpResult HandleSmallAlive(Obstacle obs)
        {
            // 1. Центр внутри границ препятствия? → удачный напрыг
            float rightTol = _hamsterWidthInUnits * RIGHT_EDGE_TOL_RATIO;
            if (CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                    _characterTransform,
                    _jumpClipWorldShift,
                    obs,
                    rightTol))
                return new JumpResult(HamsterStateEnum.JumpOnObstacle, obs);

            // 2. Иначе: есть ли вообще X-пересечение? → урон
            if (CollisionUtils.IsOverlapAtShift(
                    _characterTransform,
                    _hamsterWidthInUnits,
                    _jumpClipWorldShift,
                    obs))
                return new JumpResult(HamsterStateEnum.JumpDamageForSmallAlive, obs);

            // 3. Проверяем, перепрыгнули ли полностью
            if (CollisionUtils.IsJumpOver(
                    _characterTransform,
                    _hamsterWidthInUnits,
                    _jumpClipWorldShift,
                    obs))
                return new JumpResult(HamsterStateEnum.JumpOver, obs);

            // 4. Вообще не задели
            return _noHit;
        }

        private JumpResult HandleSmallNotAliveRoad(Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, obs))
                return new JumpResult(HamsterStateEnum.JumpDamageForSmallNotAlive, obs);

            if (CollisionUtils.IsJumpOver(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, obs))
                return new JumpResult(HamsterStateEnum.JumpOver, obs);

            return _noHit;
        }

        private JumpResult HandleSmallNotAliveRoadAndRoof(Obstacle small)
        {
            if (CollisionUtils.IsJumpOver(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, small))
                return new JumpResult(HamsterStateEnum.JumpOver, small);

            if (!CollisionUtils.IsOverlapAtShift(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, small))
                return _noHit;                                     // вовсе не столкнулись

            // проверяем: лежит ли small на крыше bigNotAlive
            // Нашли bigNotAlive под small → сохраняем в LastObstacle.
            if (CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive(small,
                                                                   _sameLineObstacles,
                                                                   out var big))
            {
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, _sameLineObstacles);
                var state = hitSmall ? HamsterStateEnum.JumpOnRoofDamage : HamsterStateEnum.JumpOnRoof;
                return new JumpResult(state, big);
            }

            // иначе — это «на дороге» вариант
            return new JumpResult(HamsterStateEnum.JumpDamageForSmallNotAlive, small);
        }

        private JumpResult HandleBigAlive(Obstacle obs)
        {
            if (IsHitXY(obs))
                return new JumpResult(HamsterStateEnum.JumpDamageForBigAlive, obs);

            return _noHit;
        }

        private JumpResult HandleBigNotAlive(Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, obs))
            {
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_characterTransform, _hamsterWidthInUnits, _jumpClipWorldShift, _sameLineObstacles);
                var state = hitSmall ? HamsterStateEnum.JumpOnRoofDamage : HamsterStateEnum.JumpOnRoof;
                return new JumpResult(state, obs);
            }

            return _noHit;
        }

        // ───── Помощники пересечений ───────────────────────────────────────────

        /// <summary>Для bigAlive: столкновение, если пересечение по X или по Y.</summary>
        private bool IsHitXY(Obstacle obs)
        {
            // пересечение по X в конце клипа
            bool hitX = CollisionUtils.IsOverlapAtShift(
                _characterTransform,
                _hamsterWidthInUnits,
                _jumpClipWorldShift,
                obs);

            // пересечение по Y в середине клипа
            CollisionUtils.GetObstacleYInterval(obs, out var oB, out var oT);
            CollisionUtils.GetHamsterYIntervalAtJumpMid(
                _characterTransform,
                _hamsterHeightInUnits,
                _jumpClipHalfY,
                out var hB,
                out var hT);

            bool hitY = CollisionUtils.IsOverlap(hB, hT, oB, oT);

            return hitX || hitY;
        }


    }
}
