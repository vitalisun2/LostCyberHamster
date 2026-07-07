using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
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
    /// Механика супер-прыжка.
    /// На основе энергии, положения хомяка и расположения препятствий
    /// вычисляет финальный <see cref="HamsterStateEnum"/> и запускает анимацию.
    /// </summary>
    public class SuperJumpMechanics
    {
        // ──────────────────────── constants ────────────────────────
        private const int ENERGY_COST_SUPER_JUMP = 10; // дополнительное усилие; Jump уже списал 10
        private const string CLIP_SUPER_JUMP = "transform_super_jump";

        // ──────────────────────── injected refs ────────────────────
        private readonly AtomicEvent _superJumpRequest;
        private readonly AtomicVariable<int> _energy;
        private readonly AtomicVariable<bool> _isOnBottomLine;
        private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly AtomicVariable<bool> _isDamaged;
        private readonly TransformAnimatorController _transformAnimatorController;
        private readonly SpriteAnimatorController _spriteAnimatorController;
        private readonly Transform _characterTransform;
        private readonly AtomicVariable<Obstacle> _lastObstacle;
        private readonly AtomicVariable<Obstacle> _pendingJumpedOnObstacle;

        // ──────────────────────── cached geometry ──────────────────
        private readonly float _hamsterWidth;
        private readonly float _superJumpShift;
        private readonly List<JumpObstacleData> _superJumpObstacleBuffer = new(32);
        private readonly JumpResult _noHit = new(HamsterStateEnum.SuperJump, null);

        public SuperJumpMechanics(
            AtomicEvent superJumpRequest,
            AtomicVariable<int> energy,
            AtomicVariable<bool> isOnBottomLine,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isDamaged,
            TransformAnimatorController transformAnimatorController,
            SpriteAnimatorController spriteAnimatorController,
            Transform characterTransform,
            AtomicVariable<Obstacle> lastObstacle,
            AtomicVariable<Obstacle> pendingJumpedOnObstacle,
            float hamsterWidthInUnits)
        {
            _superJumpRequest = superJumpRequest;
            _energy = energy;
            _isOnBottomLine = isOnBottomLine;
            _hamsterState = hamsterState;
            _isDamaged = isDamaged;
            _transformAnimatorController = transformAnimatorController;
            _spriteAnimatorController = spriteAnimatorController;
            _characterTransform = characterTransform;
            _lastObstacle = lastObstacle;
            _pendingJumpedOnObstacle = pendingJumpedOnObstacle;

            _hamsterWidth = hamsterWidthInUnits;
            _superJumpShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_SUPER_JUMP);
        }

        public void OnEnable() => _superJumpRequest.Subscribe(OnSuperJump);
        public void OnDisable() => _superJumpRequest.Unsubscribe(OnSuperJump);

        /// <summary>
        /// Точка входа для супер-прыжка:
        /// • проверяем энергию; • вычисляем финальный <see cref="HamsterStateEnum"/>;
        /// • отправляем события; • запускаем анимацию.
        /// </summary>
        private void OnSuperJump()
        {
            if (_energy.Value < ENERGY_COST_SUPER_JUMP) return;

            JumpResult result = CalculateSuperJumpState();

            _hamsterState.Value = result.State;
            if (result.Target != null) _lastObstacle.Value = result.Target;
            _pendingJumpedOnObstacle.Value = result.State == HamsterStateEnum.SuperJumpOnObstacle
                ? result.Target
                : null;

            if (result.State == HamsterStateEnum.SuperJumpOnObstacle)
                GameEventsManager.ObstacleJumpedOn(result.Target!.name);

            if (result.State == HamsterStateEnum.SuperJumpOver)
                GameEventsManager.ObstacleJumpedOver(result.Target!.name);

            SwapRoofClipsIfNeeded(result);
            _transformAnimatorController.SetSuperJumpAnimationTrigger(_hamsterState);
            _spriteAnimatorController.Jump();
        }

        private void SwapRoofClipsIfNeeded(JumpResult result)
        {
            bool isMedium = result.Target != null &&
                            result.Target.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
            _transformAnimatorController.SwapRoofClips(isMedium);
        }

        /// <summary>
        /// Определяет итог super jump через общий outcome-resolver и возвращает runtime target obstacle.
        /// </summary>
        private JumpResult CalculateSuperJumpState()
        {
            if (_isDamaged.Value) return _noHit;

            var obstacles = CollisionUtils.GetValidObstaclesAhead(_characterTransform, _isOnBottomLine.Value);
            _superJumpObstacleBuffer.Clear();

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = obstacles[obstacleIndex];
                CollisionUtils.GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out float leftX, out float rightX);
                CollisionUtils.GetObstacleYInterval(obstacle, out float bottomY, out float topY);

                _superJumpObstacleBuffer.Add(new JumpObstacleData(
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

            JumpResolveContext context = new(
                _isOnBottomLine.Value,
                hamsterLeftX,
                hamsterRightX,
                _characterTransform.position.x,
                _hamsterWidth,
                _superJumpShift,
                _superJumpShift);

            JumpResolveResult result = SuperJumpOutcomeResolver.ResolveSuperJump(_superJumpObstacleBuffer, context);
            Obstacle target = result.TargetIndex >= 0 && result.TargetIndex < obstacles.Count
                ? obstacles[result.TargetIndex]
                : null;

            if (BotDiagnostics.IsEnabled(BotDiagnosticCategory.RuntimeSafety, BotDiagnosticLevel.Verbose))
            {
                BotDiagnostics.Log(
                    BotDiagnosticCategory.RuntimeSafety,
                    BotDiagnosticLevel.Verbose,
                    $"[SUPER_JUMP_DIAG] outcome state={result.State} targetIndex={result.TargetIndex} " +
                    $"target={FormatObstacle(target)} lane={(_isOnBottomLine.Value ? "bottom" : "top")} " +
                    $"hamsterX=[{hamsterLeftX:F2},{hamsterRightX:F2}] " +
                    $"centerX={_characterTransform.position.x:F2} shift={_superJumpShift:F2} energy={_energy.Value}");
            }

            return new JumpResult(result.State, target);
        }

        private static string FormatObstacle(Obstacle obstacle)
        {
            if (obstacle == null)
                return "null";

            CollisionUtils.GetObstacleXInterval(
                obstacle,
                obstacle.ColliderWidth,
                0f,
                out float obstacleLeftX,
                out float obstacleRightX);

            return $"{obstacle.ObstacleType.ObstacleTypeEnum}#" +
                   $"{obstacle.GetInstanceID()} " +
                   $"x=[{obstacleLeftX:F2},{obstacleRightX:F2}] " +
                   $"lane={(obstacle.ObstacleType.IsTop ? "top" : "bottom")}";
        }
    }
}
