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
    /// Обработчик действия Jump: отправляет JumpRequest и ждёт приземления.
    /// Умеет задерживать прыжок, пока зона приземления занята (ShouldDelay).
    /// </summary>
    internal class JumpHandler : IActionHandler, IPreFireDelayHandler
    {
        private float _jumpWorldShift = -1f;

        public void Fire(Hamster hamster, BranchStep step)
        {
            if (hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                hamster.RoofJumpRequest.Invoke();
            else
                hamster.JumpRequest.Invoke();
        }

        public bool IsCompleted(Hamster hamster, BranchStep step)
        {
            return !IsActiveJumpState(hamster.HamsterState.Value);
        }

        public bool ShouldDelay(Hamster hamster, BranchStep step)
        {
            // Только для малых неживых препятствий
            var target = step.TargetObstacle;
            if (target.Type != ObstacleTypeEnum.smallNotAliveRoad &&
                target.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                return false;

            // Найти live instance и закешировать jump shift
            var liveObstacle = LiveObstacleFinder.Find(target.StableId);
            if (liveObstacle == null)
                return false;

            EnsureJumpWorldShiftCached(hamster);
            if (_jumpWorldShift <= 0f)
                return false;

            // Проверить, будет ли overlap в зоне приземления
            bool wouldOverlap = CollisionUtils.IsOverlapAtShift(
                hamster.transform,
                hamster.ColliderWidth,
                _jumpWorldShift,
                liveObstacle);

            if (!wouldOverlap)
                return false;

            // Отложить, если ещё есть запас дистанции
            float liveDist = liveObstacle.transform.position.x
                           - liveObstacle.ColliderWidth * 0.5f
                           - hamster.RightX;

            return liveDist > BotConsts.JumpLateFallbackDistance;
        }

        private void EnsureJumpWorldShiftCached(Hamster hamster)
        {
            if (_jumpWorldShift >= 0f)
                return;

            var ctrl = hamster.GetComponentInChildren<TransformAnimatorController>();
            if (ctrl == null)
                return;

            _jumpWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, BotConsts.JumpClipName);
        }

        private static bool IsActiveJumpState(HamsterStateEnum state)
        {
            switch (state)
            {
                case HamsterStateEnum.Jump:
                case HamsterStateEnum.JumpOver:
                case HamsterStateEnum.JumpOnObstacle:
                case HamsterStateEnum.JumpOnRoof:
                case HamsterStateEnum.JumpFromRoof:
                case HamsterStateEnum.JumpOnObstacleFromRoof:
                case HamsterStateEnum.RoofJump:
                // Damage-исходы прыжка: состояние устанавливается в момент Fire,
                // но анимация ещё не завершена — ждём animation event.
                case HamsterStateEnum.JumpDamageForSmallNotAlive:
                case HamsterStateEnum.JumpDamageForSmallAlive:
                case HamsterStateEnum.JumpDamageForBigAlive:
                case HamsterStateEnum.JumpOnRoofDamage:
                case HamsterStateEnum.JumpFromRoofDamage:
                case HamsterStateEnum.RoofJumpDamage:
                    return true;
                default:
                    return false;
            }
        }
    }
}
