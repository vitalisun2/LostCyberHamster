using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Execution.Handlers
{
    /// <summary>
    /// Выполняет и отслеживает действие смены линии через одиночный tap.
    /// </summary>
    internal sealed class SwitchLaneActionHandler : IActionExecutionHandler
    {
        /// <summary>
        /// Запускает смену линии, когда препятствие дошло до рассчитанной точки.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Validate the planned action against the current runtime state first.
            if (hamster == null || action == null || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            Obstacle obstacle = FindLiveObstacle(action.TargetObstacleInstanceId.Value);
            if (obstacle == null)
                return ActionFireResult.Cancelled;

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return ActionFireResult.Cancelled;

            // Wait until the obstacle reaches the planned trigger point.
            if (collider.bounds.min.x > action.TriggerX)
                return ActionFireResult.Waiting;

            DebugManager.DiagLog(
                $"[BotV2 EXEC] FIRE kind={action.Kind} " +
                $"triggerX={action.TriggerX:F2} obstacleLeftX={collider.bounds.min.x:F2} " +
                $"targetLane={(action.TargetBottomLine.HasValue ? (action.TargetBottomLine.Value ? "bottom" : "top") : "n/a")} " +
                $"desc={action.Description}");
            hamster.TapRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет, завершилась ли смена линии для текущего действия.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Missing runtime state means there is nothing left to wait for.
            if (hamster == null || action == null)
                return true;

            if (hamster.IsShifting.Value)
                return false;

            if (!action.TargetBottomLine.HasValue)
                return true;

            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed)
            {
                DebugManager.DiagLog(
                    $"[BotV2 EXEC] COMPLETE kind={action.Kind} " +
                    $"lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
                    $"desc={action.Description}");
            }

            return completed;
        }

        private static Obstacle FindLiveObstacle(int instanceId)
        {
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

            // Match the runtime obstacle by instance id inside the live spawner snapshot.
            for (int obstacleIndex = 0; obstacleIndex < spawner.SpawnedObstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = spawner.SpawnedObstacles[obstacleIndex]?.ObstacleScript;
                if (obstacle != null && obstacle.GetInstanceID() == instanceId)
                    return obstacle;
            }

            return null;
        }
    }
}
