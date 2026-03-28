using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Обработчик действия SwitchLane: отправляет TapRequest и ждёт завершения смены полосы.
    /// </summary>
    internal class SwitchLaneHandler : IActionHandler
    {
        private float _execTime;

        public void Fire(Hamster hamster, BranchStep step)
        {
            hamster.TapRequest.Invoke();
            _execTime = Time.time;
        }

        public bool IsCompleted(Hamster hamster, BranchStep step)
        {
            bool timeElapsed = Time.time - _execTime >= BotExecutionConsts.SwitchLaneMinElapsed;
            if (!timeElapsed || hamster.IsShifting.Value)
                return false;

            ValidateCompletionContract(step);
            return true;
        }

        public bool ShouldDelay(Hamster hamster, BranchStep step) => false;

        private void ValidateCompletionContract(BranchStep step)
        {
            float plannedTravel = step.CompletionWorldShift - step.FireWorldShift;
            if (plannedTravel <= 0f)
                return;

            float expectedDuration = plannedTravel / BotPhysicsConsts.GameSpeedBase;
            float actualDuration = Time.time - _execTime;
            float delta = actualDuration - expectedDuration;

            if (Mathf.Abs(delta) <= BotExecutionConsts.SwitchLaneCompletionTolerance)
                return;

            Debug.LogError(
                $"[BotV3 CONTRACT] SwitchLane completion drift detected. " +
                $"planned={expectedDuration:F3}s actual={actualDuration:F3}s delta={delta:F3}s " +
                $"reason={step.Reason}");
        }
    }
}
