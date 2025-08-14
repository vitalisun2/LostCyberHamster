using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly float _hamsterWidth;
        private readonly float _roofJumpShift;
        private readonly float _jumpFromRoofShift;

        private const string LOG = "[RoofJumpMechanics]";

        // список препятствий, уже лежащих на нужной линии
        private List<Obstacle> _sameLineObstacles;

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
            _sameLineObstacles = new List<Obstacle>();

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
            float hamsterHalf = _hamsterWidth * 0.5f;

            _sameLineObstacles = ObstacleSpawner.Instance.SpawnedObstacles
                                   .Select(io => io.ObstacleScript)
                                   .Where(o => HelpMethods.IsOnSameLine(_isOnBottomLine.Value, o))
                                   .ToList();

            var obstacles = _sameLineObstacles;

            foreach (var obs in obstacles)
            {
                float dx = obs.transform.position.x - _transform.position.x;
                if (dx <= 0) continue;

                // «дальше всех» уходим в конце клипа JumpFromRoof
                if (dx > hamsterHalf + _jumpFromRoofShift) break;

                if (_handlers.TryGetValue(obs.ObstacleType.ObstacleTypeEnum, out var handler))
                {
                    var res = handler(obs);
                    if (res.State != _noHit.State)      // хендлер что-то нашёл
                        return res;
                }
            }

            return _noHit;     // ничего не зацепили
        }

        // ───── Обработчики ─────────────────────────────────────────────────────────
        // bigNotAlive → RoofJump
        private JumpResult HandleBigNotAlive(Obstacle obs)
        {
            CollisionUtils.GetObstacleXInterval(obs, obs.ColliderWidth, out var oL, out var oR);
            CollisionUtils.GetHamsterXIntervalAtJumpEnd(_transform, _hamsterWidth,
                _roofJumpShift,
                out var hL, out var hR);

            if (!CollisionUtils.IsOverlap(hL, hR, oL, oR))
                return _noHit;

            bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_transform, _hamsterWidth, _roofJumpShift, _sameLineObstacles);
            Debug.Log($"{LOG} [Big] hitSmallOnRoof={hitSmall}");
            var state = hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump;

            return new JumpResult(state, obs);
        }

        // bigAlive → JumpOnObstacleFromRoof
        private JumpResult HandleBigAlive(Obstacle obs)
        {
            CollisionUtils.GetObstacleXInterval(obs, obs.ColliderWidth,
                                                out var oL, out var oR);
            CollisionUtils.GetHamsterXIntervalAtJumpEnd(_transform, _hamsterWidth,
                                                        _jumpFromRoofShift,
                                                        out var hL, out var hR);

            if (CollisionUtils.IsOverlap(hL, hR, oL, oR))
                return new JumpResult(HamsterStateEnum.JumpOnObstacleFromRoof, obs);

            return _noHit;
        }

        // smallAlive → JumpOnObstacleFromRoof
        private JumpResult HandleSmallAlive(Obstacle obs)
        {
            CollisionUtils.GetObstacleXInterval(obs, obs.ColliderWidth,
                                                out var oL, out var oR);
            CollisionUtils.GetHamsterXIntervalAtJumpEnd(_transform, _hamsterWidth,
                                                        _jumpFromRoofShift,
                                                        out var hL, out var hR);

            if (CollisionUtils.IsOverlap(hL, hR, oL, oR))
                return new JumpResult(HamsterStateEnum.JumpOnObstacleFromRoof, obs);

            return _noHit;
        }

        // smallNotAliveRoad → JumpFromRoofDamage
        private JumpResult HandleSmallNotAliveRoad(Obstacle obs)
        {
            CollisionUtils.GetObstacleXInterval(obs, obs.ColliderWidth,
                                                out var oL, out var oR);
            CollisionUtils.GetHamsterXIntervalAtJumpEnd(_transform, _hamsterWidth,
                                                        _jumpFromRoofShift,
                                                        out var hL, out var hR);

            if (CollisionUtils.IsOverlap(hL, hR, oL, oR))
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
                Debug.Log($"{LOG} [SmallR&R] onRoof: hitSmall={hitSmall}, roofShift={_roofJumpShift:F3}");
                return new JumpResult(hitSmall ? HamsterStateEnum.RoofJumpDamage : HamsterStateEnum.RoofJump, bigUnderSmall);
            }

            // ---- small стоит на дороге (прыгаем «с крыши вниз») ----
            CollisionUtils.GetHamsterXIntervalAtJumpEnd(
                _transform, _hamsterWidth, _jumpFromRoofShift,
                out var hEndL, out var hEndR);

            CollisionUtils.GetObstacleXInterval(small, small.ColliderWidth,
                                                out var oL2, out var oR2);

            if (CollisionUtils.IsOverlap(hEndL, hEndR, oL2, oR2))
                return new JumpResult(HamsterStateEnum.JumpFromRoofDamage, small);

            return _noHit;
        }

    }
}
