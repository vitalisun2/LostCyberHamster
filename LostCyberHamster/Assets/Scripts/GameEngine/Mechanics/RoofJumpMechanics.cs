using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class RoofJumpMechanics
    {
        private AtomicEvent _roofJumpRequest;
        private AtomicVariable<HamsterStateEnum> _hamsterState;
        private TransformAnimatorController _transformAnimatorController;
        private SpriteAnimatorController _spriteAnimatorController;
        private AtomicVariable<bool> _isOnBottomLine;
        private AtomicVariable<Obstacle> _lastObstacle;
        private readonly Transform _transform;
        private readonly AtomicVariable<int> _energy;

        private const string CLIP_ROOF_JUMP = "transform_roof_jump";
        private const string CLIP_JUMP_FROM_ROOF = "transform_jump_from_roof";
        private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // 20 % ширины хомяка

        private readonly float _hamsterWidth;
        private readonly float _roofJumpShift;
        private readonly float _jumpFromRoofShift;

        // список препятствий, уже лежащих на нужной линии
        private IReadOnlyList<Obstacle> _sameLineObstacles;

        private readonly Dictionary<ObstacleTypeEnum, Func<Obstacle, JumpResult>> _handlers;
        private readonly JumpResult _noHit = new(HamsterStateEnum.JumpFromRoof, null);


        public RoofJumpMechanics(AtomicEvent roofJumpRequest,
            AtomicVariable<HamsterStateEnum> hamsterState,
            TransformAnimatorController transformAnimatorController,
            SpriteAnimatorController spriteAnimatorController,
            Transform transform,
            AtomicVariable<bool> isOnBottomLine,
            AtomicVariable<Obstacle> lastObstacle,
            AtomicVariable<int> energy,
            float hamsterWidthInUnits)
        {
            _roofJumpRequest = roofJumpRequest;
            _hamsterState = hamsterState;
            _transformAnimatorController = transformAnimatorController;
            _spriteAnimatorController = spriteAnimatorController;
            _transform = transform;
            _isOnBottomLine = isOnBottomLine;
            _lastObstacle = lastObstacle;
            _energy = energy;

            _hamsterWidth = hamsterWidthInUnits;

            _roofJumpShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_ROOF_JUMP);
            _jumpFromRoofShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_JUMP_FROM_ROOF);

            // будет заполнено при вычислении состояния прыжка
            _sameLineObstacles = Array.Empty<Obstacle>();

            _handlers = new()
            {
                { ObstacleTypeEnum.bigNotAlive,              HandleBigNotAlive              },
                { ObstacleTypeEnum.bigAlive,                 HandleBigAlive                 },
                { ObstacleTypeEnum.smallAlive,               HandleSmallAlive               },
                { ObstacleTypeEnum.smallNotAliveRoad,        HandleSmallNotAliveRoad        },
                { ObstacleTypeEnum.smallNotAliveRoadAndRoof, HandleSmallNotAliveRoadAndRoof }
            };
        }

        public void OnEnable()
        {
            _roofJumpRequest.Subscribe(OnRoofJump);
        }

        public void OnDisable()
        {
            _roofJumpRequest.Unsubscribe(OnRoofJump);
        }

        private void OnRoofJump()
        {
            if (_energy.Value < 10) return;

            var result = CalculateRoofJumpState();
            _hamsterState.Value = result.State;

            if (result.Target != null) _lastObstacle.Value = result.Target;

            if (result.State == HamsterStateEnum.JumpOnObstacleFromRoof)
                GameEventsManager.ObstacleJumpedOn(result.Target!.name);

            _transformAnimatorController.SetRoofJumpAnimationTrigger(_hamsterState);
            _spriteAnimatorController.Jump();
        }

        private JumpResult CalculateRoofJumpState()
        {
            var obstacles = CollisionUtils.GetValidObstaclesAhead(_transform, _isOnBottomLine.Value);
            _sameLineObstacles = obstacles;
            float reachShift = Mathf.Max(_jumpFromRoofShift, _roofJumpShift); // максимум из двух клипов

            foreach (var obs in obstacles)
            {
                // ✅ корректный ранний выход по левой грани препятствия
                if (CollisionUtils.ShouldBreakByReachRight(_transform, _hamsterWidth, reachShift, obs))
                    break;

                if (_handlers.TryGetValue(obs.ObstacleType.ObstacleTypeEnum, out var handler))
                {
                    var res = handler(obs);
                    if (res.State != _noHit.State)
                        return res;
                }
            }

            return _noHit;
        }

        // ───── Обработчики ─────────────────────────────────────────────────────────
        // bigNotAlive → RoofJump
        private JumpResult HandleBigNotAlive(Obstacle obs)
        {
            if (!CollisionUtils.IsOverlapAtShift(_transform, _hamsterWidth, _roofJumpShift, obs))
                return _noHit;

            bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_transform, _hamsterWidth, _roofJumpShift, _sameLineObstacles);
            var state = hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump;

            return new JumpResult(state, obs);
        }

        // bigAlive → JumpOnObstacleFromRoof / JumpFromRoofDamage
        private JumpResult HandleBigAlive(Obstacle obs)
        {
            // 1. Центр внутри границ препятствия? → удачный напрыг
            float rightTol = _hamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                    _transform,
                    _jumpFromRoofShift,
                    obs,
                    rightTol))
                return new JumpResult(HamsterStateEnum.JumpOnObstacleFromRoof, obs);

            // 2. Иначе: есть ли вообще X-пересечение? → урон
            if (CollisionUtils.IsOverlapAtShift(
                    _transform,
                    _hamsterWidth,
                    _jumpFromRoofShift,
                    obs))
                return new JumpResult(HamsterStateEnum.JumpFromRoofDamage, obs);

            // 3. Вообще не задели
            return _noHit;
        }

        // smallAlive → JumpOnObstacleFromRoof / JumpFromRoofDamage
        private JumpResult HandleSmallAlive(Obstacle obs)
        {
            // 1. Центр внутри границ препятствия? → удачный напрыг
            float rightTol = _hamsterWidth * RIGHT_EDGE_TOL_RATIO;
            if (CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                    _transform,
                    _jumpFromRoofShift,
                    obs,
                    rightTol))
                return new JumpResult(HamsterStateEnum.JumpOnObstacleFromRoof, obs);

            // 2. Иначе: есть ли X-пересечение? → урон
            if (CollisionUtils.IsOverlapAtShift(
                    _transform,
                    _hamsterWidth,
                    _jumpFromRoofShift,
                    obs))
                return new JumpResult(HamsterStateEnum.JumpFromRoofDamage, obs);

            // 3. Вообще не задели
            return _noHit;
        }

        // smallNotAliveRoad → JumpFromRoofDamage
        private JumpResult HandleSmallNotAliveRoad(Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(_transform, _hamsterWidth, _jumpFromRoofShift, obs))
                return new JumpResult(HamsterStateEnum.JumpFromRoofDamage, obs);

            return _noHit;
        }

        // smallNotAliveRoadAndRoof → RoofJump / RoofJumpDamage / JumpFromRoofDamage
        private JumpResult HandleSmallNotAliveRoadAndRoof(Obstacle small)
        {
            bool isOnBigRoof = CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive(
                                   small, _sameLineObstacles, out var bigUnderSmall);

            if (isOnBigRoof)
            {
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_transform, _hamsterWidth, _roofJumpShift, _sameLineObstacles);
                return new JumpResult(hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump, bigUnderSmall);
            }

            // ---- small стоит на дороге (прыгаем «с крыши вниз») ----
            if (CollisionUtils.IsOverlapAtShift(_transform, _hamsterWidth, _jumpFromRoofShift, small))
                return new JumpResult(HamsterStateEnum.JumpFromRoofDamage, small);

            return _noHit;
        }

    }
}
