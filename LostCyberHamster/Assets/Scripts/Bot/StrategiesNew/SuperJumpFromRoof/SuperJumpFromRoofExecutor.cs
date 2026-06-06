using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.StrategiesNew.SuperJumpFromRoof
{
    /// <summary>
    /// Выполняет super-прыжок с крыши на дорогу как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperJumpFromRoofExecutor : IActionExecutionHandler
    {
        private const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        private readonly ActionTriggerGate _triggerGate;
        private bool _isUpgradeScheduled;
        private float _upgradeReadyTime;

        /// <summary>
        /// Создает executor с gate проверки live trigger obstacle.
        /// </summary>
        public SuperJumpFromRoofExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить roof jump request и запланировать super roof upgrade.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет вход и сбрасывает прошлый upgrade schedule.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            ResetUpgradeSchedule();

            // Проверяет action contract, ресурс и roof-run состояние.
            if (action.Kind != BotActionKind.SuperJumpFromRoof
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

            // Планирует второй input, если runtime перешел в roof jump состояние.
            if (!CanUpgradeToSuperRoofJump(hamster.HamsterState.Value))
            {
                HamsterActionLogger.LogCancel(action, $"stateAfterRoofJump={hamster.HamsterState.Value}");
                return ActionFireResult.Cancelled;
            }

            _isUpgradeScheduled = true;
            _upgradeReadyTime = Time.time + UpgradeDelaySeconds;
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Выполняет отложенный upgrade и проверяет завершение возвратом в Run.
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

            // Завершает action после приземления на дорогу.
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
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
            _isUpgradeScheduled = false;
            _upgradeReadyTime = 0f;
        }
    }
}
