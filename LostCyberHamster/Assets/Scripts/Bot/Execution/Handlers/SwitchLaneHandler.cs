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

            return true;
        }
    }
}
