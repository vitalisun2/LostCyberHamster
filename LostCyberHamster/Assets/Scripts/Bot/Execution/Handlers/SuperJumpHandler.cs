using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Обработчик действия SuperJump: эмулирует двойной тап игрока.
    /// Fire() — первый тап (JumpRequest, Run → hamster_jump).
    /// Update() — отложенный второй тап (SuperJumpRequest) на середине окна DoubleJumpDetector (0.15 с).
    /// </summary>
    internal class SuperJumpHandler : IActionHandler, IUpdatableHandler
    {
        private const float SecondTapDelay = 0.15f; // половина DoubleJumpDetector.DoubleJumpThreshold (0.3)

        private float _fireTime;
        private bool _secondTapFired;

        public void Fire(Hamster hamster, BranchStep step)
        {
            _fireTime = Time.time;
            _secondTapFired = false;
            hamster.JumpRequest.Invoke();
        }

        public void Update(Hamster hamster, BranchStep step)
        {
            if (_secondTapFired)
                return;

            if (Time.time - _fireTime < SecondTapDelay)
                return;

            _secondTapFired = true;
            hamster.SuperJumpRequest.Invoke();
            DebugManager.DiagLog($"[Bot EXEC] SuperJump second tap fired, delay={Time.time - _fireTime:F3}s");
        }

        public bool IsCompleted(Hamster hamster, BranchStep step)
        {
            if (!_secondTapFired)
                return false;

            var state = hamster.HamsterState.Value;
            return !IsActiveSuperJumpState(state);
        }

        private static bool IsActiveSuperJumpState(HamsterStateEnum state)
        {
            switch (state)
            {
                case HamsterStateEnum.SuperJump:
                case HamsterStateEnum.SuperJumpOver:
                case HamsterStateEnum.SuperJumpOnObstacle:
                case HamsterStateEnum.SuperJumpOnRoof:
                case HamsterStateEnum.SuperJumpDamage:
                case HamsterStateEnum.SuperJumpOnRoofDamage:
                    return true;
                default:
                    return false;
            }
        }
    }
}
