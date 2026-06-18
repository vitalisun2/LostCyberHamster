using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.PassiveCollect
{
    /// <summary>
    /// Исполняет passive collect как no-input ожидание pickup.
    /// </summary>
    internal sealed class PassiveCollectExecutor : IActionExecutionHandler
    {
        private readonly LiveObstacleResolver _liveObstacleResolver;

        public PassiveCollectExecutor(LiveObstacleResolver liveObstacleResolver)
        {
            _liveObstacleResolver = liveObstacleResolver;
        }

        /// <summary>
        /// Запускает no-input ожидание collectable pickup.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            if (hamster == null
                || action == null
                || action.Kind != BotActionKind.PassiveCollect
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            if (hamster.HamsterState.Value == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
            {
                HamsterActionLogger.LogCancel(
                    action,
                    $"state={hamster.HamsterState.Value} isDamaged={hamster.IsDamaged.Value}");
                return ActionFireResult.Cancelled;
            }

            if (!TryGetTargetBounds(action, out Bounds targetBounds))
            {
                HamsterActionLogger.LogCancel(action, "target-not-found");
                return ActionFireResult.Cancelled;
            }

            HamsterActionLogger.LogFire(
                action,
                targetBounds.min.x,
                $"collectible={action.CollectibleObjectiveValue.Kind} value={action.CollectibleObjectiveValue.EffectiveGain} ");
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Завершает ожидание, когда collectable подобран или окно pickup уже прошло.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || action.Kind != BotActionKind.PassiveCollect)
                return false;

            if (hamster.HamsterState.Value == HamsterStateEnum.Dead || hamster.IsDamaged.Value)
            {
                HamsterActionLogger.LogCancel(
                    action,
                    $"state={hamster.HamsterState.Value} isDamaged={hamster.IsDamaged.Value}");
                return true;
            }

            if (!TryGetTargetBounds(action, out Bounds targetBounds))
            {
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
                return true;
            }

            if (hamster.RightX > targetBounds.max.x)
            {
                HamsterActionLogger.LogCancel(
                    action,
                    $"missed-pickup hamsterRight={hamster.RightX:F2} targetRight={targetBounds.max.x:F2}");
                return true;
            }

            return false;
        }

        private bool TryGetTargetBounds(PlannedAction action, out Bounds bounds)
        {
            bounds = default;
            if (!action.TargetObstacleInstanceId.HasValue)
                return false;

            Obstacle target = _liveObstacleResolver.Find(action.TargetObstacleInstanceId.Value);
            if (target == null)
                return false;

            BoxCollider2D collider = target.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return false;

            bounds = collider.bounds;
            return true;
        }
    }
}
