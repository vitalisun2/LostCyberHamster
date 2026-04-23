using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
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
            // Проверяем исполнимость запланированного tap.
            Guard.NotNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            if (!action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            if (!action.TargetBottomLine.HasValue)
                return ActionFireResult.Cancelled;

            if (!TapOutcomeResolver.CanAcceptTap(
                    hamster.HamsterState.Value,
                    hamster.IsShifting.Value))
            {
                return hamster.IsShifting.Value
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

            bool targetBottomLineAfterTap = !hamster.IsOnBottomLine.Value;
            if (targetBottomLineAfterTap != action.TargetBottomLine.Value)
                return ActionFireResult.Cancelled;

            // Ждём live obstacle в рассчитанной точке запуска.
            Obstacle obstacle = FindLiveObstacle(action.TargetObstacleInstanceId.Value);
            if (obstacle == null)
                return ActionFireResult.Cancelled;

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return ActionFireResult.Cancelled;

            if (collider.bounds.min.x > action.TriggerX)
                return ActionFireResult.Waiting;

            // Отправляем tap в runtime.
            DebugManager.DiagLog(
                $"[Bot EXEC] FIRE kind={action.Kind} " +
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
            // Проверяем, достигнут ли ожидаемый результат.
            if (hamster.IsShifting.Value)
                return false;

            if (!action.TargetBottomLine.HasValue)
                return true;

            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed)
            {
                DebugManager.DiagLog(
                    $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                    $"lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
                    $"desc={action.Description}");
            }

            return completed;
        }

        private static Obstacle FindLiveObstacle(int instanceId)
        {
            // Ищем runtime obstacle по instance id.
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

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
