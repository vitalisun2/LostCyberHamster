using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared.Execution
{
    /// <summary>
    /// Проверяет, дошёл ли live obstacle до рассчитанной точки запуска action.
    /// </summary>
    internal sealed class ActionTriggerGate
    {
        private readonly LiveObstacleResolver _liveObstacleResolver;

        public ActionTriggerGate(LiveObstacleResolver liveObstacleResolver)
        {
            _liveObstacleResolver = liveObstacleResolver;
        }

        public ActionFireResult Check(PlannedAction action, out float obstacleLeftX)
        {
            obstacleLeftX = 0f;
            int? triggerObstacleInstanceId = action?.TriggerObstacleInstanceId ?? action?.TargetObstacleInstanceId;
            if (!triggerObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            Obstacle obstacle = _liveObstacleResolver.Find(triggerObstacleInstanceId.Value);
            if (obstacle == null)
                return ActionFireResult.Cancelled;

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return ActionFireResult.Cancelled;

            obstacleLeftX = collider.bounds.min.x;
            return obstacleLeftX > action.TriggerX
                ? ActionFireResult.Waiting
                : ActionFireResult.Fired;
        }
    }
}
