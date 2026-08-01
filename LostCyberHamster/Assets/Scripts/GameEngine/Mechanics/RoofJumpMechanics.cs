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
        private readonly AtomicVariable<Obstacle> _pendingJumpedOnObstacle;
        private readonly Transform _transform;
        private readonly AtomicVariable<int> _energy;

        private const string CLIP_ROOF_JUMP = "transform_roof_jump";
        private const string CLIP_JUMP_FROM_ROOF = "transform_jump_from_roof";

        private readonly float _hamsterWidth;
        private readonly float _roofJumpShift;
        private readonly float _jumpFromRoofShift;
        private readonly List<JumpObstacleData> _roofJumpObstacleBuffer = new(32);

        public RoofJumpMechanics(AtomicEvent roofJumpRequest,
            AtomicVariable<HamsterStateEnum> hamsterState,
            TransformAnimatorController transformAnimatorController,
            SpriteAnimatorController spriteAnimatorController,
            Transform transform,
            AtomicVariable<bool> isOnBottomLine,
            AtomicVariable<Obstacle> lastObstacle,
            AtomicVariable<Obstacle> pendingJumpedOnObstacle,
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
            _pendingJumpedOnObstacle = pendingJumpedOnObstacle;
            _energy = energy;

            _hamsterWidth = hamsterWidthInUnits;

            _roofJumpShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_ROOF_JUMP);
            _jumpFromRoofShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_JUMP_FROM_ROOF);
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

            Obstacle sourceRoof = _lastObstacle.Value;
            var result = CalculateRoofJumpState();
            _hamsterState.Value = result.State;

            if (result.Target != null) _lastObstacle.Value = result.Target;
            _pendingJumpedOnObstacle.Value = result.State == HamsterStateEnum.JumpOnObstacleFromRoof
                ? result.Target
                : null;

            if (result.State == HamsterStateEnum.JumpOnObstacleFromRoof)
            {
                GameEventsManager.ObstacleJumpedOnFromRoof();
            }

            ApplyRoofClips(sourceRoof, result);
            _transformAnimatorController.SetRoofJumpAnimationTrigger(_hamsterState);
            _spriteAnimatorController.Jump();
        }

        private void ApplyRoofClips(Obstacle sourceRoof, JumpResult result)
        {
            if (TrySwapRoofClipsWithHeightTransition(sourceRoof, result))
                return;

            SwapRoofClipsIfNeeded(result);
        }

        private bool TrySwapRoofClipsWithHeightTransition(Obstacle sourceRoof, JumpResult result)
        {
            if (sourceRoof == null || result.Target == null)
                return false;

            return _transformAnimatorController.TrySwapRoofClipsWithHeightTransition(
                sourceRoof.ObstacleType.ObstacleTypeEnum,
                result.Target.ObstacleType.ObstacleTypeEnum,
                CLIP_ROOF_JUMP);
        }

        private void SwapRoofClipsIfNeeded(JumpResult result)
        {
            bool isMedium = result.Target != null &&
                            result.Target.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
            _transformAnimatorController.SwapRoofClips(isMedium);
        }

        /// <summary>
        /// Определяет итог roof jump через общий outcome-resolver и возвращает runtime target obstacle.
        /// </summary>
        private JumpResult CalculateRoofJumpState()
        {
            var obstacles = CollisionUtils.GetValidObstaclesAhead(_transform, _isOnBottomLine.Value);
            _roofJumpObstacleBuffer.Clear();

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = obstacles[obstacleIndex];
                CollisionUtils.GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out float leftX, out float rightX);

                _roofJumpObstacleBuffer.Add(new JumpObstacleData(
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
                _roofJumpShift,
                _jumpFromRoofShift);

            JumpResolveResult result = RoofJumpOutcomeResolver.ResolveRoofJump(_roofJumpObstacleBuffer, context);
            Obstacle target = result.TargetIndex >= 0 && result.TargetIndex < obstacles.Count
                ? obstacles[result.TargetIndex]
                : null;

            return new JumpResult(result.State, target);
        }
    }
}
