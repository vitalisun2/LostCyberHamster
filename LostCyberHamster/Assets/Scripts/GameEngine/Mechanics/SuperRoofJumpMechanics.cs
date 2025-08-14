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
    /// <summary>
    /// Механика супер-прыжка с крыши (bigNotAlive).
    /// Определяет итоговое состояние хомяка при прыжке и инициирует нужную анимацию.
    /// </summary>
    public sealed class SuperRoofJumpMechanics
    {
        // ─────────────────────── constants ───────────────────────
        private const int REQUIRED_ENERGY = 10;
        private const string CLIP_SUPER_ROOF_JUMP = "transform_super_roof_jump";
        private const string CLIP_SUPER_JUMP_FROM_ROOF = "transform_super_jump_from_roof";

        // ─────────────────────── injected refs ───────────────────
        private readonly AtomicEvent _superRoofJumpRequest;
        private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly TransformAnimatorController _transformAnimatorController;
        private readonly SpriteAnimatorController _spriteAnimatorController;
        private readonly AtomicVariable<bool> _isOnBottomLine;
        private readonly AtomicVariable<Obstacle> _lastObstacle;
        private readonly Transform _transform;
        private readonly AtomicVariable<int> _energy;

        // ─────────────────────── cached geometry ──────────────────
        private readonly float _hamsterWidth;
        private readonly float _roofSuperJumpShift;
        private readonly float _jumpFromRoofShift;

        private const string LOG = "[SuperRoofJumpMechanics]";

        // список препятствий, уже лежащих на нужной линии
        private List<Obstacle> _sameLineObstacles;

        // ─────────────────────── handlers ─────────────────────────
        private readonly Dictionary<ObstacleTypeEnum, Func<Obstacle, JumpResult>> _handlers;
        private readonly JumpResult _noHit = new(HamsterStateEnum.SuperJumpFromRoof, null);

        public SuperRoofJumpMechanics(
            AtomicEvent superRoofJumpRequest,
            AtomicVariable<HamsterStateEnum> hamsterState,
            TransformAnimatorController transformAnimatorController,
            SpriteAnimatorController spriteAnimatorController,
            Transform transform,
            AtomicVariable<bool> isOnBottomLine,
            AtomicVariable<Obstacle> lastObstacle,
            AtomicVariable<int> energy,
            float hamsterWidthInUnits)
        {
            _superRoofJumpRequest = superRoofJumpRequest;
            _hamsterState = hamsterState;
            _transformAnimatorController = transformAnimatorController;
            _spriteAnimatorController = spriteAnimatorController;
            _transform = transform;
            _isOnBottomLine = isOnBottomLine;
            _lastObstacle = lastObstacle;
            _energy = energy;

            _hamsterWidth = hamsterWidthInUnits;
            _roofSuperJumpShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_SUPER_ROOF_JUMP);
            _jumpFromRoofShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_SUPER_JUMP_FROM_ROOF);

            // будет заполнено при вычислении состояния прыжка
            _sameLineObstacles = new List<Obstacle>();

            // словарь «тип → хендлер»
            _handlers = new()
            {
                { ObstacleTypeEnum.bigNotAlive,              HandleBigNotAlive              },
                { ObstacleTypeEnum.bigAlive,                 HandleBigAlive                 },
                { ObstacleTypeEnum.smallAlive,               HandleSmallAlive               },
                { ObstacleTypeEnum.smallNotAliveRoad,        HandleSmallNotAliveRoad        },
                { ObstacleTypeEnum.smallNotAliveRoadAndRoof, HandleSmallNotAliveRoadAndRoof }
            };
        }

        // ─────────────────── subscription lifecycle ──────────────
        public void OnEnable() => _superRoofJumpRequest.Subscribe(OnRoofSuperJump);
        public void OnDisable() => _superRoofJumpRequest.Unsubscribe(OnRoofSuperJump);

        // ─────────────────── main entrypoint ─────────────────────
        private void OnRoofSuperJump()
        {
            if (_energy.Value < REQUIRED_ENERGY) return;

            JumpResult result = CalculateRoofSuperJumpState();
            _hamsterState.Value = result.State;
            if (result.Target != null) _lastObstacle.Value = result.Target;

            if (result.State == HamsterStateEnum.SuperJumpOnObstacleFromRoof)
                GameEventsManager.ObstacleJumpedOn(result.Target!.name);

            // стейты, для которых нужно проиграть анимацию
            if (result.State is HamsterStateEnum.SuperRoofJump
                or HamsterStateEnum.SuperRoofJumpDamage
                or HamsterStateEnum.SuperJumpOnObstacleFromRoof
                or HamsterStateEnum.SuperJumpFromRoofDamage
                or HamsterStateEnum.SuperJumpFromRoof)
            {
                _transformAnimatorController.SetSuperRoofJumpAnimationTrigger(_hamsterState);
                _spriteAnimatorController.Jump();
            }
        }

        /// <summary>Определяет итог супер-прыжка, перебирая препятствия спереди.</summary>
        private JumpResult CalculateRoofSuperJumpState()
        {
            float hamsterX = _transform.position.x;
            float hamsterHalf = _hamsterWidth * 0.5f;

            _sameLineObstacles = ObstacleSpawner.Instance.SpawnedObstacles
                                   .Select(io => io.ObstacleScript)
                                   .Where(o => HelpMethods.IsOnSameLine(_isOnBottomLine.Value, o))
                                   .ToList();

            var obstacles = _sameLineObstacles;

            foreach (var obs in obstacles)
            {
                if (obs.transform.position.x <= hamsterX) continue;

                float dx = obs.transform.position.x - hamsterX;
                if (dx > hamsterHalf + Mathf.Max(_jumpFromRoofShift, _roofSuperJumpShift)) break;

                if (_handlers.TryGetValue(obs.ObstacleType.ObstacleTypeEnum, out var handler))
                {
                    JumpResult res = handler(obs);
                    if (res.State != _noHit.State) return res;
                }
            }
            return _noHit;
        }

        // ─────────────────── handlers ────────────────────────────

        private JumpResult HandleBigNotAlive(Obstacle obs)
        {
            if (!CollisionUtils.IsOverlapAtShift(_transform,
                                               _hamsterWidth,
                                               _roofSuperJumpShift,
                                               obs)) return _noHit;

            bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_transform, _hamsterWidth, _roofSuperJumpShift, _sameLineObstacles);
            Debug.Log($"{LOG} [Big] hitSmallOnRoof={hitSmall}");
            var state = hitSmall ? HamsterStateEnum.SuperRoofJumpDamage : HamsterStateEnum.SuperRoofJump;

            return new JumpResult(state, obs);
        }

        private JumpResult HandleBigAlive(Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(_transform,
                                               _hamsterWidth,
                                               _jumpFromRoofShift,
                                               obs))
                return new JumpResult(HamsterStateEnum.SuperJumpOnObstacleFromRoof, obs);

            return _noHit;
        }

        private JumpResult HandleSmallAlive(Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(_transform,
                                               _hamsterWidth,
                                               _jumpFromRoofShift,
                                               obs))
                return new JumpResult(HamsterStateEnum.SuperJumpOnObstacleFromRoof, obs);

            return _noHit;
        }

        private JumpResult HandleSmallNotAliveRoad(Obstacle obs)
        {
            if (CollisionUtils.IsOverlapAtShift(_transform,
                                               _hamsterWidth,
                                               _jumpFromRoofShift,
                                               obs))
                return new JumpResult(HamsterStateEnum.SuperJumpFromRoofDamage, obs);

            return _noHit;
        }

        // smallNotAliveRoadAndRoof → SuperRoofJump / SuperRoofJumpDamage / SuperJumpFromRoofDamage
        private JumpResult HandleSmallNotAliveRoadAndRoof(Obstacle small)
        {
            bool isOnBigRoof = CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive(
                                   small, _sameLineObstacles, out var bigUnderSmall);

            if (isOnBigRoof)
            {
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_transform, _hamsterWidth, _roofSuperJumpShift, _sameLineObstacles);
                Debug.Log($"{LOG} [SmallR&R] onRoof: hitSmall={hitSmall}, roofShift={_roofSuperJumpShift:F3}");
                return new JumpResult(hitSmall ? HamsterStateEnum.SuperRoofJumpDamage : HamsterStateEnum.SuperRoofJump, bigUnderSmall);
            }

            CollisionUtils.GetHamsterXIntervalAtJumpEnd(
                _transform, _hamsterWidth, _jumpFromRoofShift,
                out var hEndL, out var hEndR);

            CollisionUtils.GetObstacleXInterval(small, small.ColliderWidth,
                                                out var oL2, out var oR2);

            if (CollisionUtils.IsOverlap(hEndL, hEndR, oL2, oR2))
                return new JumpResult(HamsterStateEnum.SuperJumpFromRoofDamage, small);

            return _noHit;
        }
    }
}
