using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoof
{
    /// <summary>
    /// Выполняет super-прыжок с крыши на дорогу как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperJumpFromRoofExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Задержка второго input внутри окна double jump.
        /// </summary>
        private const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        /// <summary>
        /// Проверяет момент fire относительно live trigger obstacle.
        /// </summary>
        private readonly ActionTriggerGate _triggerGate;

        private bool _isUpgradeScheduled;
        private float _upgradeReadyTime;

        public SuperJumpFromRoofExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается выполнить roof jump request и запланировать super roof upgrade.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет обязательный вход.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            // Сбрасывает предыдущий незавершенный upgrade schedule.
            ResetUpgradeSchedule();

            // Проверяет action kind и target.
            if (action.Kind != BotActionKind.SuperJumpFromRoof || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            // Проверяет энергию.
            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            // Проверяет roof-run состояние.
            if (hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
                return ActionFireResult.Cancelled;

            // Проверяет fire gate.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            // Отправляет первый runtime input.
            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.RoofJumpRequest.Invoke();

            // Планирует второй input, если runtime перешел в состояние roof jump.
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