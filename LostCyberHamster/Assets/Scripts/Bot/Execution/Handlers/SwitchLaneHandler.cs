using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
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
            bool timeElapsed = Time.time - _execTime >= BotConsts.SwitchLaneMinElapsed;
            if (!timeElapsed || hamster.IsShifting.Value)
                return false;

            ValidateCompletionContract(step);
            return true;
        }

        /// <summary>
        /// Проверяет, что фактическая длительность SwitchLane совпадает с запланированной.
        /// </summary>
        private void ValidateCompletionContract(BranchStep step)
        {
            // Вычислить ожидаемую и фактическую длительность
            float plannedTravel = step.CompletionWorldShift - step.FireWorldShift;
            if (plannedTravel <= 0f)
                return;

            float expectedDuration = plannedTravel / BotConsts.GameSpeedBase;
            float actualDuration = Time.time - _execTime;
            float delta = actualDuration - expectedDuration;

            // Логировать ошибку, если дрифт за пределами допуска
            if (Mathf.Abs(delta) <= BotConsts.SwitchLaneCompletionTolerance)
                return;

            Debug.LogError(
                $"[Bot CONTRACT] SwitchLane completion drift detected. " +
                $"planned={expectedDuration:F3}s actual={actualDuration:F3}s delta={delta:F3}s " +
                $"reason={step.Reason}");
        }
    }
}
