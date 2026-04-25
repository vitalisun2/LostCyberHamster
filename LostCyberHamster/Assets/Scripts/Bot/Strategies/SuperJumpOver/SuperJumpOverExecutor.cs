using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Выполняет super jump как двухфазный runtime input.
    /// </summary>
    internal sealed class SuperJumpOverExecutor : IActionExecutionHandler
    {
        private const float UpgradeDelaySeconds = DoubleJumpDetector.DoubleJumpThreshold * 0.5f;

        private readonly ActionTriggerGate _triggerGate;
        private bool _isUpgradeScheduled;
        private float _upgradeReadyTime;

        public SuperJumpOverExecutor(ActionTriggerGate triggerGate)
        {
            _triggerGate = triggerGate;
        }

        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            Guard.NotNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            ResetUpgradeSchedule();

            if (action.Kind != BotActionKind.SuperJumpOver || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

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

            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
            if (completed)
            {
                ResetUpgradeSchedule();
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
            }

            return completed;
        }

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

        private void ResetUpgradeSchedule()
        {
            _isUpgradeScheduled = false;
            _upgradeReadyTime = 0f;
        }
    }
}
