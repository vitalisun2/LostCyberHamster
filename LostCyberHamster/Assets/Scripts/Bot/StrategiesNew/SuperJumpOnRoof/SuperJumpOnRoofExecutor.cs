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

namespace Assets.Scripts.Bot.StrategiesNew.SuperJumpOnRoof
{
    /// <summary>
    /// Выполняет super-jump-on-roof как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperJumpOnRoofExecutor : IActionExecutionHandler
    {
        private const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        private readonly ActionTriggerGate _triggerGate;
        private bool _isUpgradeScheduled;
        private float _upgradeReadyTime;

        /// <summary>
        /// Создает executor с gate проверки live trigger obstacle.
        /// </summary>
        public SuperJumpOnRoofExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается запустить super-jump-on-roof и планирует второй input.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            ResetUpgradeSchedule();
            if (action.Kind != BotActionKind.SuperJumpOnRoof
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.JumpRequest.Invoke();

            if (!CanUpgradeToSuperJump(hamster.HamsterState.Value))
            {
                HamsterActionLogger.LogCancel(action, $"stateAfterJump={hamster.HamsterState.Value}");
                return ActionFireResult.Cancelled;
            }

            _isUpgradeScheduled = true;
            _upgradeReadyTime = Time.time + UpgradeDelaySeconds;
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Выполняет отложенный upgrade input и ждёт RoofRun.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (_isUpgradeScheduled)
            {
                if (Time.time < _upgradeReadyTime)
                    return false;

                if (CanUpgradeToSuperJump(hamster.HamsterState.Value))
                    hamster.SuperJumpRequest.Invoke();

                ResetUpgradeSchedule();
            }

            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
            {
                ResetUpgradeSchedule();
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
            }

            return completed;
        }

        /// <summary>
        /// Проверяет, допускает ли текущее состояние второй input для super jump.
        /// </summary>
        private static bool CanUpgradeToSuperJump(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.Jump
                   || hamsterState == HamsterStateEnum.JumpOver
                   || hamsterState == HamsterStateEnum.JumpOnObstacle
                   || hamsterState == HamsterStateEnum.JumpOnRoof
                   || hamsterState == HamsterStateEnum.JumpDamageForSmallAlive
                   || hamsterState == HamsterStateEnum.JumpDamageForSmallNotAlive
                   || hamsterState == HamsterStateEnum.JumpDamageForBigAlive
                   || hamsterState == HamsterStateEnum.JumpOnRoofDamage;
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
