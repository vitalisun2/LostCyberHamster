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

namespace Assets.Scripts.Bot.Strategies.SuperJumpOn
{
    /// <summary>
    /// Выполняет ground super-jump-on как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperJumpOnExecutor : IActionExecutionHandler
    {
        /// <summary>
        /// Задержка между первым jump input и вторым input для upgrade до super-jump.
        /// </summary>
        private const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        /// <summary>
        /// Проверяет, наступил ли момент запуска action относительно live obstacle.
        /// </summary>
        private readonly ActionTriggerGate _triggerGate;

        /// <summary>
        /// Признак запланированного второго input для upgrade.
        /// </summary>
        private bool _isUpgradeScheduled;

        /// <summary>
        /// Runtime-время, после которого можно отправить второй input.
        /// </summary>
        private float _upgradeReadyTime;

        public SuperJumpOnExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        /// <summary>
        /// Пытается запустить super-jump-on и планирует второй input для upgrade.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяет входные данные.
            Guard.ThrowIfNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            // Сбрасывает устаревший upgrade.
            ResetUpgradeSchedule();

            // Проверяет совместимость action.
            if (action.Kind != BotActionKind.SuperJumpOn || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            // Дожидается состояния, в котором можно прыгнуть.
            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            // Проверяет запас энергии.
            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            // Проверяет live trigger.
            ActionFireResult triggerResult = _triggerGate.Check(action, out float obstacleLeftX);
            if (triggerResult != ActionFireResult.Fired)
                return triggerResult;

            // Отправляет первый runtime input.
            HamsterActionLogger.LogFire(action, obstacleLeftX);
            hamster.JumpRequest.Invoke();

            // Проверяет, что первый input перевёл хомяка в upgrade-состояние.
            if (!CanUpgradeToSuperJump(hamster.HamsterState.Value))
            {
                HamsterActionLogger.LogCancel(action, $"stateAfterJump={hamster.HamsterState.Value}");
                return ActionFireResult.Cancelled;
            }

            // Планирует второй input.
            _isUpgradeScheduled = true;
            _upgradeReadyTime = Time.time + UpgradeDelaySeconds;
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Выполняет отложенный upgrade input и проверяет завершение super-jump-on.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Обрабатывает запланированный upgrade.
            if (_isUpgradeScheduled)
            {
                if (Time.time < _upgradeReadyTime)
                    return false;

                if (CanUpgradeToSuperJump(hamster.HamsterState.Value))
                    hamster.SuperJumpRequest.Invoke();

                ResetUpgradeSchedule();
            }

            // Проверяет возврат в Run.
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
            if (completed)
            {
                ResetUpgradeSchedule();
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
            }

            return completed;
        }

        /// <summary>
        /// Проверяет, допускает ли текущее состояние второй input для перехода в super-jump.
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
