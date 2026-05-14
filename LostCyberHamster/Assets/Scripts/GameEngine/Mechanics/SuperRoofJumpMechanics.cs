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
    /// <summary>
    /// Механика супер-прыжка с крыши (bigNotAlive).
    /// Определяет итоговое состояние хомяка при прыжке и инициирует нужную анимацию.
    /// </summary>
    public sealed class SuperRoofJumpMechanics
    {
        // ─────────────────────── constants ───────────────────────
        private const int REQUIRED_ENERGY = 20;
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
        private readonly List<JumpObstacleData> _superRoofJumpObstacleBuffer = new(32);

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
                SwapRoofClipsIfNeeded(result);
                _transformAnimatorController.SetSuperRoofJumpAnimationTrigger(_hamsterState);
                _spriteAnimatorController.Jump();
            }
        }

        private void SwapRoofClipsIfNeeded(JumpResult result)
        {
            bool isMedium = result.Target != null &&
                            result.Target.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
            _transformAnimatorController.SwapRoofClips(isMedium);
        }

        /// <summary>
        /// Определяет итог super roof jump через общий outcome-resolver и возвращает runtime target obstacle.
        /// </summary>
        private JumpResult CalculateRoofSuperJumpState()
        {
            var obstacles = CollisionUtils.GetValidObstaclesAhead(_transform, _isOnBottomLine.Value);
            _superRoofJumpObstacleBuffer.Clear();

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = obstacles[obstacleIndex];
                CollisionUtils.GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out float leftX, out float rightX);

                _superRoofJumpObstacleBuffer.Add(new JumpObstacleData(
                    obstacle.ObstacleType.ObstacleTypeEnum,
                    !obstacle.ObstacleType.IsTop,
                    leftX,
                    rightX,
                    obstacle.transform.position.x));
            }

            CollisionUtils.GetHamsterXBounds(_transform, out float hamsterLeftX, out float hamsterRightX);
            RoofJumpResolveContext context = new(
                _isOnBottomLine.Value,
                hamsterLeftX,
                hamsterRightX,
                _transform.position.x,
                _hamsterWidth,
                _roofSuperJumpShift,
                _jumpFromRoofShift);

            JumpResolveResult result = SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump(
                _superRoofJumpObstacleBuffer,
                context);
            Obstacle target = result.TargetIndex >= 0 && result.TargetIndex < obstacles.Count
                ? obstacles[result.TargetIndex]
                : null;

            return new JumpResult(result.State, target);
        }
    }
}
