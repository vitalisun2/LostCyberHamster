using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.PassiveAdvance
{
    /// <summary>
    /// Исполняет passive advance как no-input ожидание ухода boundary obstacle.
    /// </summary>
    internal sealed class PassiveAdvanceExecutor : IActionExecutionHandler
    {
        private const float CompletionEpsilon = 0.01f;

        private readonly LiveObstacleResolver _liveObstacleResolver;

        public PassiveAdvanceExecutor(LiveObstacleResolver liveObstacleResolver)
        {
            _liveObstacleResolver = liveObstacleResolver;
        }

        /// <summary>
        /// Фиксирует начало no-input ожидания.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            if (hamster == null
                || action == null
                || action.Kind != BotActionKind.PassiveAdvance
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            if (!CanAdvance(hamster, action))
                return ActionFireResult.Cancelled;

            float obstacleLeftX = TryGetTargetBounds(action, out Bounds targetBounds)
                ? targetBounds.min.x
                : action.RenderWorldX;
            HamsterActionLogger.LogFire(action, obstacleLeftX);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Завершает ожидание, когда boundary obstacle ушел за левую границу хомяка.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || action.Kind != BotActionKind.PassiveAdvance)
                return false;

            if (!CanAdvance(hamster, action))
                return true;

            if (!TryGetTargetBounds(action, out Bounds targetBounds))
            {
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
                return true;
            }

            if (targetBounds.max.x <= hamster.LeftX + CompletionEpsilon)
            {
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
                return true;
            }

            return false;
        }

        private static bool CanAdvance(Hamster hamster, PlannedAction action)
        {
            HamsterStateEnum state = hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Run && !hamster.IsDamaged.Value)
                return true;

            HamsterActionLogger.LogCancel(
                action,
                $"state={state} isDamaged={hamster.IsDamaged.Value}");
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
