using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.StrategiesNew.SuperRoofJumpOver
{
    /// <summary>
    /// Выполняет super roof jump-over как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperRoofJumpOverExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Задержка второго input для upgrade до super roof jump.
        /// </summary>
        private const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        /// <summary>
        /// Gate проверки live trigger obstacle перед отправкой input.
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

        /// <summary>
        /// Создает executor с gate проверки live trigger obstacle.
        /// </summary>
        public SuperRoofJumpOverExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить первый roof-jump input и запланировать upgrade до super.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет вход и сбрасывает старый upgrade state.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            ResetUpgradeSchedule();

            // Проверяет action contract и runtime state.
            if (action.Kind != BotActionKind.SuperRoofJumpOver
                || !action.TargetObstacleInstanceId.HasValue
                || hamster.Energy.Value < action.EnergyCost
                || hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
            {
                return ActionFireResult.Cancelled;
            }

            // Проверяет live trigger и отправляет первый input.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

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
            _upgradeReadyTime = Time.time + UpgradeDelaySeconds;
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Отправляет второй input в upgrade-window и ждет возврата в RoofRun.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Выполняет отложенный upgrade input.
            if (_isUpgradeScheduled)
            {
                if (Time.time < _upgradeReadyTime)
                    return false;

                if (CanUpgradeToSuperRoofJump(hamster.HamsterState.Value))
                    hamster.SuperRoofJumpRequest.Invoke();

                ResetUpgradeSchedule();
            }

            // Проверяет возврат runtime state в RoofRun.
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
            {
                ResetUpgradeSchedule();
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
            }

            return completed;
        }

        /// <summary>
        /// Возвращает true, если текущее состояние допускает upgrade до super roof jump.
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
        /// Сбрасывает состояние отложенного upgrade input.
        /// </summary>
        private void ResetUpgradeSchedule()
        {
            // Очищает schedule второго input.
            _isUpgradeScheduled = false;
            _upgradeReadyTime = 0f;
        }
    }
}
