using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.StrategiesNew.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Выполняет super-прыжок с текущей крыши на следующую крышу как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperJumpFromRoofOnRoofExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Gate проверки live trigger roof перед отправкой input.
        /// </summary>
        private readonly ActionTriggerGate _triggerGate;

        /// <summary>
        /// Признак запланированного второго input.
        /// </summary>
        private bool _isUpgradeScheduled;

        /// <summary>
        /// Runtime-время, когда можно отправить второй input.
        /// </summary>
        private float _upgradeReadyTime;

        public SuperJumpFromRoofOnRoofExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить roof jump request и запланировать super roof upgrade.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет обязательный вход и сбрасывает старый upgrade state.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            ResetUpgradeSchedule();

            // Проверяет action contract и runtime state.
            if (action.Kind != BotActionKind.SuperJumpFromRoofOnRoof
                || !action.TargetObstacleInstanceId.HasValue
                || hamster.Energy.Value < action.EnergyCost
                || hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
            {
                return ActionFireResult.Cancelled;
            }

            // Проверяет fire gate.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            // Отправляет первый runtime input.
            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.RoofJumpRequest.Invoke();

            // Проверяет, что runtime перешел в состояние, допускающее upgrade.
            if (!CanUpgradeToSuperRoofJump(hamster.HamsterState.Value))
            {
                HamsterActionLogger.LogCancel(action, $"stateAfterRoofJump={hamster.HamsterState.Value}");
                return ActionFireResult.Cancelled;
            }

            // Планирует второй input.
            _isUpgradeScheduled = true;
            _upgradeReadyTime = Time.time + SuperJumpFromRoofOnRoofTiming.UpgradeDelaySeconds;
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Выполняет отложенный upgrade и проверяет завершение возвратом в RoofRun.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Выполняет второй input, когда наступила точка upgrade.
            if (_isUpgradeScheduled)
            {
                if (Time.time < _upgradeReadyTime)
                    return false;

                if (CanUpgradeToSuperRoofJump(hamster.HamsterState.Value))
                    hamster.SuperRoofJumpRequest.Invoke();

                ResetUpgradeSchedule();
            }

            // Завершает action после посадки на крышу.
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
            {
                ResetUpgradeSchedule();
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
            }

            return completed;
        }

        /// <summary>
        /// Проверяет, допускает ли текущее состояние upgrade до super roof jump.
        /// </summary>
        private static bool CanUpgradeToSuperRoofJump(HamsterStateEnum hamsterState)
        {
            // Перечисляет runtime states между первым и вторым input.
            return hamsterState == HamsterStateEnum.RoofJump
                   || hamsterState == HamsterStateEnum.RoofJumpDamage
                   || hamsterState == HamsterStateEnum.JumpFromRoof
                   || hamsterState == HamsterStateEnum.JumpFromRoofDamage
                   || hamsterState == HamsterStateEnum.JumpOnObstacleFromRoof;
        }

        /// <summary>
        /// Сбрасывает отложенный upgrade input.
        /// </summary>
        private void ResetUpgradeSchedule()
        {
            // Очищает schedule второго input.
            _isUpgradeScheduled = false;
            _upgradeReadyTime = 0f;
        }
    }
}
