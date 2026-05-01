using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using System.Collections.Generic;
using Assets.Scripts.GameEngine.Mechanics.Models;
using UnityEngine;
using Atomic.Elements;
using Unity.Profiling;

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
        private readonly float _jumpClipWorldShift;
        private readonly float _jumpClipHalfY;

        private readonly Transform _characterTransform;
        private readonly AtomicVariable<Obstacle> _lastObstacle;
        private readonly float _hamsterWidthInUnits;
        private readonly float _hamsterHeightInUnits;
        private readonly List<JumpObstacleData> _jumpObstacleBuffer = new(32);

        private static readonly ProfilerMarker s_JumpLogicMarker = new ProfilerMarker("JumpLogic");

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
        }

        public void OnEnable() => _jumpRequest.Subscribe(OnJump);
        public void OnDisable() => _jumpRequest.Unsubscribe(OnJump);

        /// <summary>
        /// Запускает расчёт и анимацию прыжка, если хватает энергии.
        /// </summary>
        private void OnJump()
        {
            using (s_JumpLogicMarker.Auto())
            {
                if (_energy.Value < 10) return;

                var result = CalculateJumpState();
                _hamsterState.Value = result.State;
                if (result.Target != null) _lastObstacle.Value = result.Target;

                SendJumpEventIfNeeded(result);

                SwapRoofClipsIfNeeded(result);
                _transformAnimatorController.SetJumpAnimationTrigger(_hamsterState);
                _spriteAnimatorController.Jump();
            }
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

        private void SwapRoofClipsIfNeeded(JumpResult result)
        {
            bool isMedium = result.Target != null &&
                            result.Target.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
            _transformAnimatorController.SwapRoofClips(isMedium);
        }

        /// <summary>
        /// Определяет итог прыжка, группируя логику по типу препятствия.
        /// </summary>
        private JumpResult CalculateJumpState()
        {
            bool isDamaged = _isDamaged.Value;
            if (isDamaged) return _noHit;

            var obstacles = CollisionUtils.GetValidObstaclesAhead(_characterTransform, _isOnBottomLine.Value);
            _jumpObstacleBuffer.Clear();

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = obstacles[obstacleIndex];
                CollisionUtils.GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out float leftX, out float rightX);
                CollisionUtils.GetObstacleYInterval(obstacle, out float bottomY, out float topY);

                _jumpObstacleBuffer.Add(new JumpObstacleData(
                    obstacle.ObstacleType.ObstacleTypeEnum,
                    !obstacle.ObstacleType.IsTop,
                    leftX,
                    rightX,
                    obstacle.transform.position.x,
                    hasY: true,
                    bottomY: bottomY,
                    topY: topY));
            }

            CollisionUtils.GetHamsterXBounds(_characterTransform, out float hamsterLeftX, out float hamsterRightX);
            CollisionUtils.GetHamsterYIntervalAtJumpMid(
                _characterTransform,
                _hamsterHeightInUnits,
                _jumpClipHalfY,
                out float hamsterJumpMidBottomY,
                out float hamsterJumpMidTopY);

            JumpResolveContext context = new(
                _isOnBottomLine.Value,
                hamsterLeftX,
                hamsterRightX,
                _characterTransform.position.x,
                _hamsterWidthInUnits,
                _jumpClipWorldShift,
                _jumpClipWorldShift,
                hasJumpMidY: true,
                hamsterJumpMidBottomY,
                hamsterJumpMidTopY);

            JumpResolveResult result = JumpOutcomeResolver.ResolveJump(_jumpObstacleBuffer, context);
            Obstacle target = result.TargetIndex >= 0 && result.TargetIndex < obstacles.Count
                ? obstacles[result.TargetIndex]
                : null;

            return new JumpResult(result.State, target);
        }
    }
}
